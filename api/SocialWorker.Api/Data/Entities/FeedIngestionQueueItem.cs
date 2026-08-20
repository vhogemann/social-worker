using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class FeedIngestionQueueItem
{
    public Guid Id { get; set; }
    public Guid FeedSubscriptionId { get; set; }
    public FeedSubscription FeedSubscription { get; set; } = null!;
    public Guid AutomationWorkflowId { get; set; }
    public AutomationWorkflow AutomationWorkflow { get; set; } = null!;
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

    public static FeedIngestionQueueItem Create(
        Guid feedSubscriptionId,
        Guid automationWorkflowId,
        string itemTitle,
        string itemLink,
        string? itemDescription = null,
        DateTime? itemPublishedAt = null,
        int maxAttempts = 3)
    {
        return new FeedIngestionQueueItem
        {
            Id = Guid.NewGuid(),
            FeedSubscriptionId = feedSubscriptionId,
            AutomationWorkflowId = automationWorkflowId,
            ItemTitle = string.IsNullOrWhiteSpace(itemTitle) ? "Untitled Feed Item" : itemTitle.Trim(),
            ItemLink = itemLink.Trim(),
            ItemDescription = itemDescription,
            ItemPublishedAt = itemPublishedAt,
            Status = FeedQueueItemStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
