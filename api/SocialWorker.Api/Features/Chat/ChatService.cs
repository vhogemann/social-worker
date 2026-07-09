using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Chat;

public sealed class ChatService
{
    private readonly ChatSessionLoader _sessionLoader;
    private readonly SystemPromptBuilder _promptBuilder;
    private readonly ChatStreamWriter _writer;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<ChatService> _log;
    private readonly IEnumerable<IChatTool> _tools;

    public ChatService(
        ChatSessionLoader sessionLoader,
        SystemPromptBuilder promptBuilder,
        ChatStreamWriter writer,
        ILlmProviderAdapter adapter,
        ILogger<ChatService> log,
        IEnumerable<IChatTool> tools)
    {
        _sessionLoader = sessionLoader;
        _promptBuilder = promptBuilder;
        _writer = writer;
        _adapter = adapter;
        _log = log;
        _tools = tools;
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
            session.Capabilities.SupportsVision);

        var convo = new List<OpenAiModels.OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        foreach (var m in req.Messages)
        {
            var text = string.Join("\n", m.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
            convo.Add(new OpenAiModels.OpenAiMessage { Role = m.Role, Content = text });
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

        var payload = new OpenAiModels.ChatCompletionRequest
        {
            Model = session.Credentials.Model,
            Messages = convo,
            Stream = true,
            Tools = tools
        };

        yield return _writer.MessageId();

        const int maxRounds = 3;
        for (var round = 0; round < maxRounds; round++)
        {
            var roundCtx = new RoundContext(round, payload, session, req.DraftId, userId, ct);
            await foreach (var line in ProcessRoundAsync(roundCtx))
            {
                yield return line;
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
            return new ToolExecutionResult(new { error = $"unknown tool: {name}" });
        }

        try
        {
            return await tool.ExecuteRawAsync(argumentsJson, draftId, userId, ct);
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

    private sealed class AccumulatedToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}