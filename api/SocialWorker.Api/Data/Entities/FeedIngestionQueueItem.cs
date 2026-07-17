using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class FeedIngestionQueueItem
{
    public Guid Id { get; set; }
    public Guid FeedSubscriptionId { get; set; }
    public FeedSubscription FeedSubscription { get; set; } = null!;
    public string ItemTitle { get; set; } = null!;
    public string ItemLink { get; set; } = null!;
    public string? ItemDescription { get; set; }
    public DateTime? ItemPublishedAt { get; set; }
    public FeedQueueItemStatus Status { get; set; } = FeedQueueItemStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
