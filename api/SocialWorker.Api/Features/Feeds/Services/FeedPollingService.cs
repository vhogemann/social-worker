using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CodeHollow.FeedReader;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedPollingService
{
    private readonly AppDbContext _db;
    private readonly BackgroundJobQueue _jobQueue;
    private readonly ILogger<FeedPollingService> _logger;

    public FeedPollingService(
        AppDbContext db,
        BackgroundJobQueue jobQueue,
        ILogger<FeedPollingService> logger)
    {
        _db = db;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task PollAllSubscriptionsAsync(CancellationToken ct)
    {
        await DrainStalePendingItemsAsync(ct);

        var subscriptions = await _db.FeedSubscriptions.ToListAsync(ct);
        _logger.LogInformation("Polling {Count} feed subscriptions...", subscriptions.Count);

        foreach (var sub in subscriptions)
        {
            try
            {
                await PollSubscriptionAsync(sub.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll feed subscription {Title} ({Id})", sub.Title, sub.Id);
            }
        }
    }

    public async Task PollSubscriptionAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await _db.FeedSubscriptions.FindAsync(new object[] { subscriptionId }, ct);
        if (sub == null) return;

        _logger.LogInformation("Polling feed subscription: {Title} ({Url})", sub.Title, sub.FeedUrl);

        var feed = await FeedReader.ReadAsync(sub.FeedUrl);
        var lastPolled = sub.LastPolledAt ?? DateTime.UtcNow.AddDays(-1);

        var newItems = feed.Items
            .Where(item => item.PublishingDate.HasValue && item.PublishingDate.Value.ToUniversalTime() > lastPolled.ToUniversalTime())
            .OrderBy(item => item.PublishingDate)
            .ToList();

        if (!newItems.Any() && sub.LastPolledAt == null)
        {
            newItems = feed.Items.Take(3).ToList();
        }

        _logger.LogInformation("Found {Count} new items for subscription {Title}", newItems.Count, sub.Title);

        var queuedCount = 0;

        foreach (var item in newItems)
        {
            if (string.IsNullOrWhiteSpace(item.Link))
                continue;

            var exists = await _db.FeedIngestionQueueItems
                .AnyAsync(q => q.FeedSubscriptionId == subscriptionId && q.ItemLink == item.Link, ct);
            if (exists)
                continue;

            _db.FeedIngestionQueueItems.Add(new FeedIngestionQueueItem
            {
                Id = Guid.NewGuid(),
                FeedSubscriptionId = subscriptionId,
                ItemTitle = string.IsNullOrWhiteSpace(item.Title) ? "Untitled Feed Item" : item.Title,
                ItemLink = item.Link,
                ItemDescription = item.Description,
                ItemPublishedAt = item.PublishingDate,
                Status = FeedQueueItemStatus.Pending,
                AttemptCount = 0,
                MaxAttempts = 3,
                NextAttemptAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            queuedCount++;
            EnqueueProcessingJob();
        }

        _logger.LogInformation("Queued {Count} feed items for processing for subscription {Title}", queuedCount, sub.Title);

        sub.LastPolledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task DrainStalePendingItemsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleCount = await _db.FeedIngestionQueueItems
            .CountAsync(q => (q.Status == FeedQueueItemStatus.Pending || q.Status == FeedQueueItemStatus.Failed)
                             && q.AttemptCount < q.MaxAttempts
                             && q.NextAttemptAt <= now, ct);

        for (var i = 0; i < staleCount; i++)
            EnqueueProcessingJob();

        if (staleCount > 0)
            _logger.LogInformation("Enqueued {Count} stale feed queue items for processing", staleCount);
    }

    private void EnqueueProcessingJob()
    {
        _jobQueue.EnqueueScoped("feed-ingestion", (sp, ct) =>
            sp.GetRequiredService<FeedIngestionQueueProcessor>().ProcessNextQueueItemAsync(ct));
    }
}
