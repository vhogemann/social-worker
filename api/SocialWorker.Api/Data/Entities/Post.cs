using System;

namespace SocialWorker.Api.Data.Entities;

public class Post
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;
    
    public Guid PlatformThreadId { get; set; }
    public PlatformThread PlatformThread { get; set; } = null!;
    
    public int SegmentIndex { get; set; }
    public string Platform { get; set; } = string.Empty;
    
    public string? RemoteId { get; set; }
    public string? Url { get; set; }
    public string? Error { get; set; }
    
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}
