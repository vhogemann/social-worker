using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed class ChatStreamWriter
{
    public sealed record EmptyStreamObject();

    public sealed record StreamTokenUsage(
        [property: JsonPropertyName("promptTokens")] int PromptTokens = 0,
        [property: JsonPropertyName("completionTokens")] int CompletionTokens = 0);

    public sealed record ToolCallEventPayload(
        [property: JsonPropertyName("toolCallId")] string ToolCallId,
        [property: JsonPropertyName("toolName")] string ToolName,
        [property: JsonPropertyName("args")] object Args);

    public sealed record ToolResultEventPayload(
        [property: JsonPropertyName("toolCallId")] string ToolCallId,
        [property: JsonPropertyName("result")] object Result);

    public sealed record StepFinishEventPayload(
        [property: JsonPropertyName("finishReason")] string FinishReason,
        [property: JsonPropertyName("usage")] StreamTokenUsage Usage,
        [property: JsonPropertyName("isContinued")] bool IsContinued);

    public string MessageId(string? messageId = null)
    {
        var effectiveMessageId = string.IsNullOrWhiteSpace(messageId)
            ? "m_" + Guid.NewGuid().ToString("N")
            : messageId;

        return "f:{\"messageId\":\"" + effectiveMessageId + "\"}\n";
    }

    public string TextDelta(string content)
    {
        return "0:" + JsonSerializer.Serialize(content) + "\n";
    }

    public string ToolCall(string id, string name, string argsJson)
    {
        object argsObj = string.IsNullOrEmpty(argsJson)
            ? new EmptyStreamObject()
            : JsonDocument.Parse(argsJson).RootElement.Clone();

        var payload = new ToolCallEventPayload(id, name, argsObj);
        return "9:" + JsonSerializer.Serialize(payload) + "\n";
    }

    public string ToolResult(string id, object result)
    {
        var payload = new ToolResultEventPayload(id, result);
        return "a:" + JsonSerializer.Serialize(payload) + "\n";
    }

    public string StepFinish(string finishReason, bool isContinued)
    {
        var payload = new StepFinishEventPayload(finishReason, new StreamTokenUsage(), isContinued);
        return "e:" + JsonSerializer.Serialize(payload) + "\n";
    }

    public string StreamDone()
    {
        return "d:{\"finishReason\":\"stop\",\"usage\":{\"promptTokens\":0,\"completionTokens\":0}}\n";
    }
}
