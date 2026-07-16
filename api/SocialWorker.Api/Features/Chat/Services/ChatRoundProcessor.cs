using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed class ChatRoundProcessor
{
    private readonly ChatStreamWriter _writer;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ChatToolExecutor _toolExecutor;
    private readonly ILogger<ChatRoundProcessor> _log;

    public ChatRoundProcessor(
        ChatStreamWriter writer,
        ILlmProviderAdapter adapter,
        ChatToolExecutor toolExecutor,
        ILogger<ChatRoundProcessor> log)
    {
        _writer = writer;
        _adapter = adapter;
        _toolExecutor = toolExecutor;
        _log = log;
    }

    public async IAsyncEnumerable<string> ProcessAsync(ChatRoundContext ctx)
    {
        _log.LogInformation("Starting chat stream round {Round}. Total messages: {MessageCount}", ctx.Round, ctx.Payload.Messages.Count);
        var toolCalls = new Dictionary<int, AccumulatedToolCall>();
        string? finishReason = null;
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in _adapter.CompleteStreamAsync(ctx.Payload, ctx.Session.Credentials, ctx.Ct))
        {
            foreach (var choice in chunk.Choices)
            {
                if (choice.Delta.Content is { } content)
                {
                    responseBuilder.Append(content);
                    yield return _writer.TextDelta(content);
                }

                if (choice.Delta.ToolCalls is { } deltaToolCalls)
                {
                    foreach (var toolCall in deltaToolCalls)
                    {
                        if (!toolCalls.TryGetValue(toolCall.Index, out var accumulated))
                        {
                            accumulated = new AccumulatedToolCall();
                            toolCalls[toolCall.Index] = accumulated;
                        }

                        if (toolCall.Id is { } id) accumulated.Id = id;
                        if (toolCall.Function?.Name is { } name) accumulated.Name = name;
                        if (toolCall.Function?.Arguments is { } args) accumulated.Arguments += args;
                    }
                }

                if (choice.FinishReason is { } reason)
                {
                    finishReason = reason;
                }
            }
        }

        if (responseBuilder.Length > 0)
        {
            _log.LogInformation("LLM streamed response (Round {Round}): {Response}", ctx.Round, responseBuilder.ToString());
        }

        if (toolCalls.Count == 0)
        {
            yield return _writer.StepFinish(finishReason ?? "stop", false);
            ctx.ShouldStop = true;
            yield break;
        }

        ctx.SawAnyToolCall = true;

        var assistantMessage = new OpenAiModels.OpenAiMessage
        {
            Role = "assistant",
            ToolCalls = toolCalls.Values.Select(tc => new OpenAiModels.OpenAiToolCall
            {
                Id = tc.Id,
                Function = new() { Name = tc.Name, Arguments = tc.Arguments },
            }).ToList(),
        };
        ctx.Payload.Messages.Add(assistantMessage);

        foreach (var toolCall in toolCalls.Values)
        {
            ctx.CalledToolNames.Add(toolCall.Name);

            if (string.Equals(toolCall.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                ctx.SawReplaceEditorToolCall = true;
            }

            _log.LogInformation("LLM requested tool execution: {ToolName} with arguments: {Args}", toolCall.Name, toolCall.Arguments);
            yield return _writer.ToolCall(toolCall.Id, toolCall.Name, toolCall.Arguments);

            if (ctx.EnforceAllSourcesFetchedBeforeReplace &&
                string.Equals(toolCall.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                if (!ctx.HasListedSources)
                {
                    var mustListResult = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call list_sources and then fetch_source for every listed source ID. list_sources has not been called in this request yet."
                    });
                    yield return _writer.ToolResult(toolCall.Id, mustListResult.Result);
                    ctx.Payload.Messages.AddRange(mustListResult.ToMessages(toolCall.Id));
                    continue;
                }

                var missingIds = ctx.GetMissingSourceIds();
                if (missingIds.Count > 0)
                {
                    var missingFetchResult = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call fetch_source for every listed source ID.",
                        missingSourceIds = missingIds.Select(id => id.ToString()).ToArray()
                    });
                    yield return _writer.ToolResult(toolCall.Id, missingFetchResult.Result);
                    ctx.Payload.Messages.AddRange(missingFetchResult.ToMessages(toolCall.Id));
                    continue;
                }
            }

            if (string.Equals(toolCall.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                if (ctx.EnforceImageImportBeforeReplace && !ctx.SawAddImageSource)
                {
                    var missingImageImport = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call add_image_source with a direct image URL and use the returned media:// tag."
                    });
                    yield return _writer.ToolResult(toolCall.Id, missingImageImport.Result);
                    ctx.Payload.Messages.AddRange(missingImageImport.ToMessages(toolCall.Id));
                    continue;
                }

                if (ctx.EnforceImageInspectionBeforeReplace && !ctx.SawViewImage)
                {
                    var missingImageInspection = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call view_image to inspect at least one selected image."
                    });
                    yield return _writer.ToolResult(toolCall.Id, missingImageInspection.Result);
                    ctx.Payload.Messages.AddRange(missingImageInspection.ToMessages(toolCall.Id));
                    continue;
                }
            }

            var toolResult = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, ctx.DraftId, ctx.UserId, ctx.Ct);
            _log.LogInformation("Tool {ToolName} execution completed.", toolCall.Name);
            yield return _writer.ToolResult(toolCall.Id, toolResult.ToDisplayPayload());

            if (string.Equals(toolCall.Name, "validate_draft", StringComparison.Ordinal))
            {
                yield return _writer.TextDelta(toolResult.ToDisplayText());
            }

            ctx.Payload.Messages.AddRange(toolResult.ToMessages(toolCall.Id));

            if (string.Equals(toolCall.Name, "validate_draft", StringComparison.Ordinal))
            {
                ctx.SawValidateDraftToolCall = true;
                ctx.LastValidateHadBlockingErrors = ValidateResultHasBlockingErrors(toolResult);
            }

            RecordImageTrackingState(ctx, toolCall.Name);

            if (ctx.EnforceAllSourcesFetchedBeforeReplace)
            {
                RecordSourceTrackingState(ctx, toolCall.Name, toolResult.Result);
            }
        }

        yield return _writer.StepFinish("tool-calls", true);
    }

    private static void RecordImageTrackingState(ChatRoundContext ctx, string toolName)
    {
        if (string.Equals(toolName, "add_image_source", StringComparison.Ordinal))
        {
            ctx.SawAddImageSource = true;
        }

        if (string.Equals(toolName, "view_image", StringComparison.Ordinal))
        {
            ctx.SawViewImage = true;
        }
    }

    private static void RecordSourceTrackingState(ChatRoundContext ctx, string toolName, object toolResult)
    {
        if (string.Equals(toolName, "list_sources", StringComparison.Ordinal))
        {
            if (toolResult is ListSourcesResult listResult)
            {
                ctx.ListedSourceIds.Clear();
                foreach (var item in listResult.Items)
                {
                    ctx.ListedSourceIds.Add(item.Id);
                }
                return;
            }

            if (toolResult is IEnumerable<ListSourcesResultItem> listed)
            {
                ctx.ListedSourceIds.Clear();
                foreach (var item in listed)
                {
                    ctx.ListedSourceIds.Add(item.Id);
                }
                return;
            }

            if (toolResult is JsonElement listJson && listJson.ValueKind == JsonValueKind.Array)
            {
                ctx.ListedSourceIds.Clear();
                foreach (var item in listJson.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp) &&
                        idProp.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(idProp.GetString(), out var parsedId))
                    {
                        ctx.ListedSourceIds.Add(parsedId);
                    }
                }
            }
            return;
        }

        if (string.Equals(toolName, "fetch_source", StringComparison.Ordinal))
        {
            if (toolResult is FetchSourceResult fetched)
            {
                ctx.FetchedSourceIds.Add(fetched.Id);
                return;
            }

            if (toolResult is JsonElement fetchJson &&
                fetchJson.ValueKind == JsonValueKind.Object &&
                fetchJson.TryGetProperty("id", out var idProp) &&
                idProp.ValueKind == JsonValueKind.String &&
                Guid.TryParse(idProp.GetString(), out var parsedId))
            {
                ctx.FetchedSourceIds.Add(parsedId);
            }
        }
    }

    private static bool ValidateResultHasBlockingErrors(ToolExecutionResult toolResult)
    {
        if (toolResult.Result is IChatBlockingValidationResult validationResult)
        {
            return validationResult.HasBlockingErrors;
        }

        if (toolResult.Result is string text)
        {
            return text.Contains("validation failed", StringComparison.OrdinalIgnoreCase)
                || text.Contains("**Error**", StringComparison.OrdinalIgnoreCase)
                || text.Contains("❌", StringComparison.OrdinalIgnoreCase);
        }

        if (toolResult.Result is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                var stringValue = json.GetString() ?? string.Empty;
                return stringValue.Contains("validation failed", StringComparison.OrdinalIgnoreCase)
                    || stringValue.Contains("**Error**", StringComparison.OrdinalIgnoreCase)
                    || stringValue.Contains("❌", StringComparison.OrdinalIgnoreCase);
            }

            if (json.ValueKind == JsonValueKind.Object &&
                json.TryGetProperty("overallStatus", out var status) &&
                status.ValueKind == JsonValueKind.String)
            {
                var statusText = status.GetString() ?? string.Empty;
                return statusText.Contains("failed", StringComparison.OrdinalIgnoreCase)
                    || statusText.Contains("error", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private sealed class AccumulatedToolCall
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }
}

public sealed class ChatRoundContext
{
    public int Round { get; }
    public OpenAiModels.ChatCompletionRequest Payload { get; }
    public ChatSessionContext Session { get; }
    public Guid? DraftId { get; }
    public Guid UserId { get; }
    public CancellationToken Ct { get; }
    public bool ShouldStop { get; set; }
    public bool SawAnyToolCall { get; set; }
    public bool SawReplaceEditorToolCall { get; set; }
    public bool EnforceAllSourcesFetchedBeforeReplace { get; }
    public bool EnforceImageImportBeforeReplace { get; }
    public bool EnforceImageInspectionBeforeReplace { get; }
    public HashSet<Guid> ListedSourceIds { get; }
    public HashSet<Guid> FetchedSourceIds { get; }
    public bool SawAddImageSource { get; set; }
    public bool SawViewImage { get; set; }
    public bool SawValidateDraftToolCall { get; set; }
    public bool LastValidateHadBlockingErrors { get; set; }
    public HashSet<string> CalledToolNames { get; } = new(StringComparer.Ordinal);
    public bool HasListedSources => ListedSourceIds.Count > 0;

    public ChatRoundContext(
        int round,
        OpenAiModels.ChatCompletionRequest payload,
        ChatSessionContext session,
        Guid? draftId,
        Guid userId,
        CancellationToken ct,
        bool enforceAllSourcesFetchedBeforeReplace,
        HashSet<Guid> listedSourceIds,
        HashSet<Guid> fetchedSourceIds,
        bool enforceImageImportBeforeReplace,
        bool enforceImageInspectionBeforeReplace,
        bool sawAddImageSource,
        bool sawViewImage)
    {
        Round = round;
        Payload = payload;
        Session = session;
        DraftId = draftId;
        UserId = userId;
        Ct = ct;
        EnforceAllSourcesFetchedBeforeReplace = enforceAllSourcesFetchedBeforeReplace;
        ListedSourceIds = listedSourceIds;
        FetchedSourceIds = fetchedSourceIds;
        EnforceImageImportBeforeReplace = enforceImageImportBeforeReplace;
        EnforceImageInspectionBeforeReplace = enforceImageInspectionBeforeReplace;
        SawAddImageSource = sawAddImageSource;
        SawViewImage = sawViewImage;
    }

    public List<Guid> GetMissingSourceIds()
    {
        return ListedSourceIds.Where(id => !FetchedSourceIds.Contains(id)).ToList();
    }
}