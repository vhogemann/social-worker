using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Features.Chat;

public sealed class ChatService
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex EditorUpdateIntentRegex = new(
        @"\b(write|rewrite|re-write|edit|update|improve|polish|refine|fix|revise|shorten|expand|draft|compose|create|generate|rework|apply\s+(these|the)\s+changes)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AllSourcesIntentRegex = new(
        @"\b(all|every|each)\b.{0,48}\bsources?\b|\bsources?\b.{0,48}\b(all|every|each)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImageIntentRegex = new(
        @"\b(image|images|photo|photos|picture|pictures|illustrat(e|ion|ive))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImageInspectionIntentRegex = new(
        @"\b(inspect|inspection|analy[sz]e|review|look\s+at|view)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ChatSessionLoader _sessionLoader;
    private readonly SystemPromptBuilder _promptBuilder;
    private readonly ChatStreamWriter _writer;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<ChatService> _log;
    private readonly IEnumerable<IChatTool> _tools;
    private readonly ChatOptions _chatOptions;

    public ChatService(
        ChatSessionLoader sessionLoader,
        SystemPromptBuilder promptBuilder,
        ChatStreamWriter writer,
        ILlmProviderAdapter adapter,
        ILogger<ChatService> log,
        IEnumerable<IChatTool> tools,
        IOptions<ChatOptions> chatOptions)
    {
        _sessionLoader = sessionLoader;
        _promptBuilder = promptBuilder;
        _writer = writer;
        _adapter = adapter;
        _log = log;
        _tools = tools;
        _chatOptions = chatOptions.Value;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatModels.ChatRequest req,
        Guid userId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var session = await _sessionLoader.LoadAsync(userId, req.DraftId, req.Editor, req.Messages, ct);
        var systemPrompt = _promptBuilder.Build(
            req.System,
            session.EditorContent,
            session.MediaAssets,
            session.Capabilities.SupportsVision,
            session.DefaultBrandVoiceBody);

        var finalSystemPrompt = systemPrompt;
        if (!string.IsNullOrEmpty(session.Draft.ChatSummary))
        {
            finalSystemPrompt += $"\n\nContext summary of the conversation so far:\n{session.Draft.ChatSummary}";
        }

        var tools = _tools
            .Where(t => !t.RequiresVision || session.Capabilities.SupportsVision)
            .Select(t => new OpenAiModels.OpenAiTool
            {
                Function = new()
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.Parameters
                }
            })
            .ToList();

        var commandText = ExtractLatestUserText(req.Messages);

        var historyBudget = ChatContextWindowPolicy.CalculateHistoryBudgetTokens(session.Provider.ContextWindowTokens, session.Credentials.Model, finalSystemPrompt, tools);
        var messagesToSend = ChatContextWindowPolicy.SelectRecentMessages(req.Messages, historyBudget);

        _log.LogInformation(
            "Chat context selection for model {Model}: total messages={TotalMessages}, selected messages={SelectedMessages}, estimated history budget tokens={HistoryBudget}",
            session.Credentials.Model,
            req.Messages.Count,
            messagesToSend.Count,
            historyBudget);

        var convo = new List<OpenAiModels.OpenAiMessage>
        {
            new() { Role = "system", Content = finalSystemPrompt }
        };

        foreach (var m in messagesToSend)
        {
            var text = string.Join("\n", m.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
            convo.Add(new OpenAiModels.OpenAiMessage { Role = m.Role, Content = text });
        }

        var payload = new OpenAiModels.ChatCompletionRequest
        {
            Model = session.Credentials.Model,
            Messages = convo,
            Stream = true,
            Tools = tools
        };

        yield return _writer.MessageId();

        if (TryParseSlashCommand(commandText, out var slashCommand, out var slashArgs))
        {
            var slashOutput = await ExecuteSlashCommandAsync(slashCommand, slashArgs, req.DraftId, userId, tools, ct);
            yield return _writer.TextDelta(slashOutput);
            yield return _writer.StepFinish("stop", false);
            yield return _writer.StreamDone();
            yield break;
        }

        var requiresEditorUpdate = _chatOptions.StrictEditorUpdateEnforcement && RequiresEditorUpdateTool(commandText);
        var requiresAllSourcesFetchedBeforeReplace = RequiresAllSourcesFetch(commandText);
        var requiresImageImportBeforeReplace = RequiresImageAttachmentFlow(commandText);
        var requiresImageInspectionBeforeReplace = requiresImageImportBeforeReplace && RequiresImageInspection(commandText);
        var editorEnforcementRetried = false;
        var sawReplaceEditorToolCallOverall = false;
        var sawAddImageSourceOverall = false;
        var sawViewImageOverall = false;
        var listedSourceIds = new HashSet<Guid>();
        var fetchedSourceIds = new HashSet<Guid>();
        var hasStableValidatedDraft = false;
        var finalizationNudgeInjected = false;

        var maxRounds = Math.Clamp(_chatOptions.MaxToolExecutionRounds, 1, 16);
        for (var round = 0; round < maxRounds; round++)
        {
            var roundCtx = new RoundContext(
                round,
                payload,
                session,
                req.DraftId,
                userId,
                ct,
                requiresAllSourcesFetchedBeforeReplace,
                listedSourceIds,
                fetchedSourceIds,
                requiresImageImportBeforeReplace,
                requiresImageInspectionBeforeReplace,
                sawAddImageSourceOverall,
                sawViewImageOverall);
            await foreach (var line in ProcessRoundAsync(roundCtx))
            {
                yield return line;
            }

            sawReplaceEditorToolCallOverall = sawReplaceEditorToolCallOverall || roundCtx.SawReplaceEditorToolCall;
            sawAddImageSourceOverall = roundCtx.SawAddImageSource;
            sawViewImageOverall = roundCtx.SawViewImage;

            if (roundCtx.SawValidateDraftToolCall && !roundCtx.LastValidateHadBlockingErrors && sawReplaceEditorToolCallOverall)
            {
                hasStableValidatedDraft = true;
            }

            if (hasStableValidatedDraft && !finalizationNudgeInjected)
            {
                finalizationNudgeInjected = true;
                payload.Messages.Add(new OpenAiModels.OpenAiMessage
                {
                    Role = "system",
                    Content = "FINALIZATION: The draft is already updated and validated with no blocking errors. Do not call replace_editor_content or validate_draft again unless the user explicitly asks for another revision. Provide a concise final response to the user now."
                });
                continue;
            }

            if (hasStableValidatedDraft &&
                finalizationNudgeInjected &&
                roundCtx.CalledToolNames.Any(name =>
                    string.Equals(name, "replace_editor_content", StringComparison.Ordinal) ||
                    string.Equals(name, "validate_draft", StringComparison.Ordinal)))
            {
                yield return _writer.TextDelta("Draft updated and validated in the editor.");
                yield return _writer.StepFinish("stop", false);
                break;
            }

            if (roundCtx.ShouldStop &&
                requiresEditorUpdate &&
                !sawReplaceEditorToolCallOverall &&
                !editorEnforcementRetried)
            {
                editorEnforcementRetried = true;
                payload.Messages.Add(new OpenAiModels.OpenAiMessage
                {
                    Role = "system",
                    Content = ChatPromptCatalog.Current.Chat.EditorUpdateEnforcement
                });
                continue;
            }

            if (roundCtx.ShouldStop)
            {
                break;
            }
        }

        yield return _writer.StreamDone();
    }

    private async IAsyncEnumerable<string> ProcessRoundAsync(RoundContext ctx)
    {
        _log.LogInformation("Starting chat stream round {Round}. Total messages: {MessageCount}", ctx.Round, ctx.Payload.Messages.Count);
        var toolCalls = new Dictionary<int, AccumulatedToolCall>();
        string? finishReason = null;
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in _adapter.CompleteStreamAsync(ctx.Payload, ctx.Session.Credentials, ctx.Ct))
        {
            foreach (var choice in chunk.Choices)
            {
                if (choice.Delta.Content is { } c)
                {
                    responseBuilder.Append(c);
                    yield return _writer.TextDelta(c);
                }

                if (choice.Delta.ToolCalls is { } tcs)
                {
                    foreach (var tc in tcs)
                    {
                        if (!toolCalls.TryGetValue(tc.Index, out var acc))
                        {
                            acc = new AccumulatedToolCall();
                            toolCalls[tc.Index] = acc;
                        }
                        if (tc.Id is { } id) acc.Id = id;
                        if (tc.Function?.Name is { } name) acc.Name = name;
                        if (tc.Function?.Arguments is { } args) acc.Arguments += args;
                    }
                }

                if (choice.FinishReason is { } fr)
                {
                    finishReason = fr;
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

        var assistantMsg = new OpenAiModels.OpenAiMessage
        {
            Role = "assistant",
            ToolCalls = toolCalls.Values.Select(tc => new OpenAiModels.OpenAiToolCall
            {
                Id = tc.Id,
                Function = new() { Name = tc.Name, Arguments = tc.Arguments }
            }).ToList(),
        };
        ctx.Payload.Messages.Add(assistantMsg);

        foreach (var tc in toolCalls.Values)
        {
            ctx.CalledToolNames.Add(tc.Name);

            if (string.Equals(tc.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                ctx.SawReplaceEditorToolCall = true;
            }

            _log.LogInformation("LLM requested tool execution: {ToolName} with arguments: {Args}", tc.Name, tc.Arguments);
            yield return _writer.ToolCall(tc.Id, tc.Name, tc.Arguments);

            if (ctx.EnforceAllSourcesFetchedBeforeReplace &&
                string.Equals(tc.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                if (!ctx.HasListedSources)
                {
                    var mustListResult = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call list_sources and then fetch_source for every listed source ID. list_sources has not been called in this request yet."
                    });
                    yield return _writer.ToolResult(tc.Id, mustListResult.Result);
                    ctx.Payload.Messages.AddRange(mustListResult.ToMessages(tc.Id));
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
                    yield return _writer.ToolResult(tc.Id, missingFetchResult.Result);
                    ctx.Payload.Messages.AddRange(missingFetchResult.ToMessages(tc.Id));
                    continue;
                }
            }

            if (string.Equals(tc.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                if (ctx.EnforceImageImportBeforeReplace && !ctx.SawAddImageSource)
                {
                    var missingImageImport = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call add_image_source with a direct image URL and use the returned media:// tag."
                    });
                    yield return _writer.ToolResult(tc.Id, missingImageImport.Result);
                    ctx.Payload.Messages.AddRange(missingImageImport.ToMessages(tc.Id));
                    continue;
                }

                if (ctx.EnforceImageInspectionBeforeReplace && !ctx.SawViewImage)
                {
                    var missingImageInspection = new ToolExecutionResult(new
                    {
                        error = "Before replace_editor_content, call view_image to inspect at least one selected image."
                    });
                    yield return _writer.ToolResult(tc.Id, missingImageInspection.Result);
                    ctx.Payload.Messages.AddRange(missingImageInspection.ToMessages(tc.Id));
                    continue;
                }
            }

            var toolResult = await ExecuteToolAsync(tc.Name, tc.Arguments, ctx.DraftId, ctx.UserId, ctx.Ct);
            _log.LogInformation("Tool {ToolName} execution completed.", tc.Name);
            yield return _writer.ToolResult(tc.Id, toolResult.Result);

            ctx.Payload.Messages.AddRange(toolResult.ToMessages(tc.Id));

            if (string.Equals(tc.Name, "validate_draft", StringComparison.Ordinal))
            {
                ctx.SawValidateDraftToolCall = true;
                ctx.LastValidateHadBlockingErrors = ValidateResultHasBlockingErrors(toolResult.Result);
            }

            RecordImageTrackingState(ctx, tc.Name);

            if (ctx.EnforceAllSourcesFetchedBeforeReplace)
            {
                RecordSourceTrackingState(ctx, tc.Name, toolResult.Result);
            }
        }

        yield return _writer.StepFinish("tool-calls", true);
    }

    private async Task<ToolExecutionResult> ExecuteToolAsync(
        string name,
        string argumentsJson,
        Guid? draftId,
        Guid userId,
        CancellationToken ct)
    {
        var tool = _tools.FirstOrDefault(t => t.Name == name);
        if (tool == null)
        {
            _log.LogWarning("Unknown tool call request: {ToolName}", name);
            return new ToolExecutionResult(new { error = $"unknown tool: {name}" });
        }

        try
        {
            _log.LogInformation("Executing tool {ToolName} (Draft: {DraftId}, User: {UserId}) with args: {Args}", name, draftId, userId, argumentsJson);
            var result = await tool.ExecuteRawAsync(argumentsJson, draftId, userId, ct);
            _log.LogInformation("Successfully executed tool {ToolName}. Output: {Result}", name, JsonSerializer.Serialize(result.Result));
            return result;
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            _log.LogError(inner, "Error executing tool {ToolName} with args {Args}", name, argumentsJson);
            return new ToolExecutionResult(new { error = inner.Message });
        }
    }

    private sealed class RoundContext
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

        public List<Guid> GetMissingSourceIds()
        {
            return ListedSourceIds.Where(id => !FetchedSourceIds.Contains(id)).ToList();
        }

        public RoundContext(
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
    }

    private static bool RequiresEditorUpdateTool(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith('/'))
        {
            return false;
        }

        return EditorUpdateIntentRegex.IsMatch(text);
    }

    private static bool RequiresAllSourcesFetch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return AllSourcesIntentRegex.IsMatch(text) &&
               text.Contains("fetch_source", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresImageAttachmentFlow(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ImageIntentRegex.IsMatch(text) &&
               (text.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("attach", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("illustr", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresImageInspection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ImageInspectionIntentRegex.IsMatch(text);
    }

    private static void RecordImageTrackingState(RoundContext ctx, string toolName)
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

    private static void RecordSourceTrackingState(RoundContext ctx, string toolName, object toolResult)
    {
        if (string.Equals(toolName, "list_sources", StringComparison.Ordinal))
        {
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

    private static bool ValidateResultHasBlockingErrors(object toolResult)
    {
        if (toolResult is string s)
        {
            return s.Contains("validation failed", StringComparison.OrdinalIgnoreCase)
                || s.Contains("**Error**", StringComparison.OrdinalIgnoreCase)
                || s.Contains("❌", StringComparison.OrdinalIgnoreCase);
        }

        if (toolResult is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var text = el.GetString() ?? string.Empty;
                return text.Contains("validation failed", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("**Error**", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("❌", StringComparison.OrdinalIgnoreCase);
            }

            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("overallStatus", out var status) &&
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
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }

    private static string ExtractLatestUserText(List<ChatModels.UiMessage> messages)
    {
        var latestUser = messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        if (latestUser == null)
        {
            return string.Empty;
        }

        return string.Join("\n", latestUser.Content.Where(p => p.Type == "text").Select(p => p.Text ?? string.Empty)).Trim();
    }

    private static bool TryParseSlashCommand(string text, out string command, out string args)
    {
        command = string.Empty;
        args = string.Empty;

        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
        {
            return false;
        }

        var trimmed = text.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace < 0)
        {
            command = trimmed[1..].ToLowerInvariant();
            return true;
        }

        command = trimmed[1..firstSpace].ToLowerInvariant();
        args = trimmed[(firstSpace + 1)..].Trim();
        return true;
    }

    private async Task<string> ExecuteSlashCommandAsync(
        string command,
        string args,
        Guid? draftId,
        Guid userId,
        List<OpenAiModels.OpenAiTool> visibleTools,
        CancellationToken ct)
    {
        if (command == "help" || command == "tools")
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available slash commands:");
            sb.AppendLine("- /validate");
            sb.AppendLine("- /search <query>");
            sb.AppendLine("- /search-image <query>");
            sb.AppendLine();
            sb.AppendLine("Use /search and /search-image for explicit query-driven tool execution.");
            return sb.ToString().Trim();
        }

        if (command == "validate")
        {
            var result = await ExecuteToolAsync("validate_draft", "{}", draftId, userId, ct);
            return result.Result is string s ? s : JsonSerializer.Serialize(result.Result);
        }

        if (command == "search")
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return "Usage: /search <query>. Example: /search latest Bluesky character limit";
            }

            var toolArgs = JsonSerializer.Serialize(new { query = args });
            var result = await ExecuteToolAsync("web_search", toolArgs, draftId, userId, ct);
            return result.Result is string str ? str : JsonSerializer.Serialize(result.Result);
        }

        if (command == "search-image" || command == "images")
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return "Usage: /search-image <query>. Example: /search-image pineapple product shots";
            }

            var toolArgs = JsonSerializer.Serialize(new { query = args });
            var result = await ExecuteToolAsync("image_search", toolArgs, draftId, userId, ct);
            return result.Result is string str ? str : JsonSerializer.Serialize(result.Result);
        }

        return "Unknown slash command. Use /help to see available commands.";
    }
}