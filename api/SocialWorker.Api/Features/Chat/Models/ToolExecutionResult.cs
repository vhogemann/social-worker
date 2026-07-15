using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SocialWorker.Api.Features.Chat.Models;

public sealed record ToolExecutionResult(
    object Result,
    IReadOnlyList<OpenAiModels.OpenAiMessage>? ExtraMessages = null)
{
    public IChatToolResult AsDisplayResult()
    {
        return Result as IChatToolResult ?? new DefaultChatToolResult(Result);
    }

    public string ToDisplayText()
    {
        return AsDisplayResult().ToDisplayText();
    }

    public object ToDisplayPayload()
    {
        return Result is IChatToolResult ? ToDisplayText() : Result;
    }

    public List<OpenAiModels.OpenAiMessage> ToMessages(string toolCallId)
    {
        if (ExtraMessages != null && ExtraMessages.Count > 0)
        {
            foreach (var msg in ExtraMessages)
            {
                if (msg.Role == "tool" && string.IsNullOrEmpty(msg.ToolCallId))
                {
                    msg.ToolCallId = toolCallId;
                }
            }
            return ExtraMessages.ToList();
        }

        var contentStr = Result is string s ? s : JsonSerializer.Serialize(Result);
        return new List<OpenAiModels.OpenAiMessage>
        {
            new()
            {
                Role = "tool",
                ToolCallId = toolCallId,
                Content = contentStr
            }
        };
    }

    private sealed record DefaultChatToolResult(object Value) : IChatToolResult
    {
        public string ToDisplayText()
        {
            return Value is string s ? s : JsonSerializer.Serialize(Value);
        }
    }
}
