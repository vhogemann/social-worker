using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record CommentRankingResult(
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("shouldReply")] bool ShouldReply,
    [property: JsonPropertyName("isTerminal")] bool IsTerminal,
    [property: JsonPropertyName("reasoning")] string Reasoning,
    [property: JsonPropertyName("shouldLike")] bool ShouldLike = false
)
{
    public static CommentRankingResult DefaultSkip(string reason) =>
        new(0, "Skipped", false, false, reason, false);
}

public sealed record GeneratedReplyResult(
    bool Success,
    string? ReplyText,
    string? ErrorMessage
)
{
    public static GeneratedReplyResult Succeeded(string text) => new(true, text, null);
    public static GeneratedReplyResult Failed(string error) => new(false, null, error);
}
