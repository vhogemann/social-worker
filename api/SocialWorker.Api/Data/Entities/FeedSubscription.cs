using System;

namespace SocialWorker.Api.Data.Entities;

public class FeedSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string FeedUrl { get; set; } = null!;
    public string? WebsiteUrl { get; set; }
    public string InstructionPrompt { get; set; } = null!;
    public bool AutoPublish { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public string? IncludeFilters { get; set; }
    public string? ExcludeFilters { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
