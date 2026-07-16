using System;

namespace SocialWorker.Api.Data.Entities;

public class DraftBlueskyMetadata
{
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;

    public string? ReplyRootUri { get; set; }
    public string? ReplyRootCid { get; set; }
    public string? ReplyParentUri { get; set; }
    public string? ReplyParentCid { get; set; }
    public string? ReplyParentUrl { get; set; }
    public string? ReplyParentAuthor { get; set; }
    public string? ReplyParentText { get; set; }
    public string? ReplyParentAvatarUrl { get; set; }
}