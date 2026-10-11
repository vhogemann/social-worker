using System;
using System.Collections.Generic;

namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record PlatformCommentItem(
    string Platform,
    string RemoteCommentId,
    string? RemoteParentId,
    string RemoteRootId,
    string RemoteAuthorHandle,
    string RemoteAuthorId,
    string Text,
    DateTime CreatedAt,
    int Depth,
    IReadOnlyList<string> ExtractedUrls,
    IReadOnlyList<PlatformCommentImageAttachment> ExtractedImages,
    object? PlatformMetadata = null
);

public sealed record PlatformCommentImageAttachment(
    string ImageUrl,
    string? AltText
);

public sealed record PublishReplyResult(
    bool Success,
    string? RemoteReplyId,
    string? RemoteReplyUrl,
    string? ErrorMessage
)
{
    public static PublishReplyResult Succeeded(string remoteReplyId, string? remoteReplyUrl = null) =>
        new(true, remoteReplyId, remoteReplyUrl, null);

    public static PublishReplyResult Failed(string error) =>
        new(false, null, null, error);
}
