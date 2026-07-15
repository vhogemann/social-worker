using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed class ChatToolExecutor
{
    private readonly IEnumerable<IChatTool> _tools;
    private readonly ILogger<ChatToolExecutor> _log;

    public ChatToolExecutor(IEnumerable<IChatTool> tools, ILogger<ChatToolExecutor> log)
    {
        _tools = tools;
        _log = log;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
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
            _log.LogInformation(
                "Executing tool {ToolName} (Draft: {DraftId}, User: {UserId}) with args: {Args}",
                name,
                draftId,
                userId,
                argumentsJson);
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
}