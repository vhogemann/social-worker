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

    public static FeedSubscription Create(Guid userId, Features.Feeds.CreateFeedSubscriptionRequest req)
        => Create(userId, req.Title, req.FeedUrl, req.WebsiteUrl, req.InstructionPrompt, req.AutoPublish, req.IncludeFilters, req.ExcludeFilters);

    public static FeedSubscription Create(
        Guid userId,
        string title,
        string feedUrl,
        string? websiteUrl = null,
        string? instructionPrompt = null,
        bool autoPublish = false,
        string? includeFilters = null,
        string? excludeFilters = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Subscription title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(feedUrl))
            throw new ArgumentException("Subscription feed URL is required.", nameof(feedUrl));

        return new FeedSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            FeedUrl = feedUrl.Trim(),
            WebsiteUrl = websiteUrl?.Trim(),
            InstructionPrompt = string.IsNullOrWhiteSpace(instructionPrompt) ? "Summarize this article as a thread." : instructionPrompt.Trim(),
            AutoPublish = autoPublish,
            IncludeFilters = includeFilters?.Trim(),
            ExcludeFilters = excludeFilters?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
