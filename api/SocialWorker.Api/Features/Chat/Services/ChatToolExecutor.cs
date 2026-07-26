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
        ToolExecutionContext context)
    {
        var tool = _tools.FirstOrDefault(t => t.Name == name);
        if (tool == null)
        {
            _log.LogWarning("Unknown tool call request: {ToolName}", name);
            return ToolExecutionResult.Error($"unknown tool: {name}");
        }

        try
        {
            _log.LogInformation(
                "Executing tool {ToolName} (Draft: {DraftId}, User: {UserId}) with args: {Args}",
                name,
                context.DraftId,
                context.UserId,
                argumentsJson);
            var result = await tool.ExecuteRawAsync(argumentsJson, context);
            _log.LogInformation("Successfully executed tool {ToolName}. Output: {Result}", name, JsonSerializer.Serialize(result.Result));
            return result;
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            _log.LogError(inner, "Error executing tool {ToolName} with args {Args}", name, argumentsJson);
            return ToolExecutionResult.Error(inner.Message);
        }
    }

    public sealed record ToolErrorPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] string Error);
}