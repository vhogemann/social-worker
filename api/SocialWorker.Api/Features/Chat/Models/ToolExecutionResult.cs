using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SocialWorker.Api.Features.Chat;

public sealed record ToolExecutionResult(
    object Result,
    IReadOnlyList<OpenAiModels.OpenAiMessage>? ExtraMessages = null)
{
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
}
