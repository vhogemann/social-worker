using System;

namespace SocialWorker.Api.Data.Entities;

public class Post
{
    public Guid Id { get; set; }
    public Guid? DraftId { get; set; }
    public Draft? Draft { get; set; }
    
    public Guid? PlatformThreadId { get; set; }
    public PlatformThread? PlatformThread { get; set; }
    
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    
    public int SegmentIndex { get; set; }
    public string Platform { get; set; } = string.Empty;
    
    public string? RemoteId { get; set; }
    public string? RemoteCid { get; set; }
    public string? PublishedText { get; set; }
    public string? Url { get; set; }
    public string? AuthorHandle { get; set; }
    public bool IsExternal { get; set; }
    public string? Error { get; set; }
    
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public static Post CreateInternal(
        Guid draftId,
        Guid platformThreadId,
        Guid userId,
        int segmentIndex,
        string platform,
        string? remoteId,
        string? remoteCid,
        string? publishedText,
        string? url,
        string? authorHandle = null,
        DateTime? postedAt = null)
    {
        return new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draftId,
            PlatformThreadId = platformThreadId,
            UserId = userId,
            SegmentIndex = segmentIndex,
            Platform = platform,
            RemoteId = remoteId,
            RemoteCid = remoteCid,
            PublishedText = publishedText,
            Url = url,
            AuthorHandle = authorHandle,
            IsExternal = false,
            PostedAt = postedAt ?? DateTime.UtcNow
        };
    }

    public static Post CreateExternal(
        Guid userId,
        string platform,
        string remoteId,
        string? remoteCid,
        string? publishedText,
        string? url,
        string? authorHandle,
        DateTime? postedAt = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(platform))
            throw new ArgumentException("Platform is required.", nameof(platform));
        if (string.IsNullOrWhiteSpace(remoteId))
            throw new ArgumentException("RemoteId is required.", nameof(remoteId));

        return new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DraftId = null,
            PlatformThreadId = null,
            SegmentIndex = 0,
            Platform = platform.Trim(),
            RemoteId = remoteId.Trim(),
            RemoteCid = string.IsNullOrWhiteSpace(remoteCid) ? null : remoteCid.Trim(),
            PublishedText = publishedText,
            Url = url,
            AuthorHandle = string.IsNullOrWhiteSpace(authorHandle) ? null : authorHandle.Trim(),
            IsExternal = true,
            PostedAt = postedAt ?? DateTime.UtcNow
        };
    }
}
