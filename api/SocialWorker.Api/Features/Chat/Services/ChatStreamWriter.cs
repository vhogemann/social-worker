using System;
using System.Text.Json;

namespace SocialWorker.Api.Features.Chat;

public sealed class ChatStreamWriter
{
    public string MessageId()
    {
        return "f:{\"messageId\":\"m_" + Guid.NewGuid().ToString("N") + "\"}\n";
    }

    public string TextDelta(string content)
    {
        return "0:" + JsonSerializer.Serialize(content) + "\n";
    }

    public string ToolCall(string id, string name, string argsJson)
    {
        var obj = new
        {
            toolCallId = id,
            toolName = name,
            args = string.IsNullOrEmpty(argsJson)
                ? (object)new { }
                : JsonDocument.Parse(argsJson).RootElement.Clone(),
        };
        return "9:" + JsonSerializer.Serialize(obj) + "\n";
    }

    public string ToolResult(string id, object result)
    {
        var obj = new
        {
            toolCallId = id,
            result = result
        };
        return "a:" + JsonSerializer.Serialize(obj) + "\n";
    }

    public string StepFinish(string finishReason, bool isContinued)
    {
        var obj = new
        {
            finishReason,
            usage = new { promptTokens = 0, completionTokens = 0 },
            isContinued,
        };
        return "e:" + JsonSerializer.Serialize(obj) + "\n";
    }

    public string StreamDone()
    {
        return "d:{\"finishReason\":\"stop\",\"usage\":{\"promptTokens\":0,\"completionTokens\":0}}\n";
    }
}
