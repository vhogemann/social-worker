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

namespace SocialWorker.Api.Features.Chat;

public sealed class ChatService
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex EditorUpdateIntentRegex = new(
        @"\b(write|rewrite|re-write|edit|update|improve|polish|refine|fix|revise|shorten|expand|draft|compose|create|generate|rework|apply\s+(these|the)\s+changes)\b",
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
        var editorEnforcementRetried = false;
        var sawReplaceEditorToolCallOverall = false;

        const int maxRounds = 3;
        for (var round = 0; round < maxRounds; round++)
        {
            var roundCtx = new RoundContext(round, payload, session, req.DraftId, userId, ct);
            await foreach (var line in ProcessRoundAsync(roundCtx))
            {
                yield return line;
            }

            sawReplaceEditorToolCallOverall = sawReplaceEditorToolCallOverall || roundCtx.SawReplaceEditorToolCall;

            if (roundCtx.ShouldStop &&
                requiresEditorUpdate &&
                !sawReplaceEditorToolCallOverall &&
                !editorEnforcementRetried)
            {
                editorEnforcementRetried = true;
                payload.Messages.Add(new OpenAiModels.OpenAiMessage
                {
                    Role = "system",
                    Content = "EDITOR-UPDATE ENFORCEMENT: The user's request requires directly updating draft content. You must call replace_editor_content with the full updated markdown, then call validate_draft. Do not ask for confirmation and do not only describe changes."
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
            if (string.Equals(tc.Name, "replace_editor_content", StringComparison.Ordinal))
            {
                ctx.SawReplaceEditorToolCall = true;
            }

            _log.LogInformation("LLM requested tool execution: {ToolName} with arguments: {Args}", tc.Name, tc.Arguments);
            yield return _writer.ToolCall(tc.Id, tc.Name, tc.Arguments);

            var toolResult = await ExecuteToolAsync(tc.Name, tc.Arguments, ctx.DraftId, ctx.UserId, ctx.Ct);
            _log.LogInformation("Tool {ToolName} execution completed.", tc.Name);
            yield return _writer.ToolResult(tc.Id, toolResult.Result);

            ctx.Payload.Messages.AddRange(toolResult.ToMessages(tc.Id));
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

        public RoundContext(
            int round,
            OpenAiModels.ChatCompletionRequest payload,
            ChatSessionContext session,
            Guid? draftId,
            Guid userId,
            CancellationToken ct)
        {
            Round = round;
            Payload = payload;
            Session = session;
            DraftId = draftId;
            UserId = userId;
            Ct = ct;
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