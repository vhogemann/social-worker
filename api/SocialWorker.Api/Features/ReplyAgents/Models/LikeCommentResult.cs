namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record LikeCommentResult(bool Success, string? RemoteLikeId, string? ErrorMessage = null, bool IsSupported = true)
{
    public static LikeCommentResult Succeeded(string remoteLikeId) => new(true, remoteLikeId);
    public static LikeCommentResult Failed(string errorMessage) => new(false, null, errorMessage);
    public static LikeCommentResult NotSupported(string reason) => new(false, null, reason, IsSupported: false);
}
