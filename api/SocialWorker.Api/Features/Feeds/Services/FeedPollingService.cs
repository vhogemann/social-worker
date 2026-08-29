using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CodeHollow.FeedReader;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Feeds.Handlers;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedPollingService
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<FeedPollingService> _logger;

    public FeedPollingService(
        AppDbContext db,
        IBackgroundTaskQueue queue,
        ILogger<FeedPollingService> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    public async Task PollAllSubscriptionsAsync(CancellationToken ct)
    {
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
        var sub = await _db.FeedSubscriptions
            .Include(s => s.WorkflowSources)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
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

            foreach (var wf in sub.WorkflowSources)
            {
                var deduplicationKey = $"feed:{subscriptionId}:{wf.AutomationWorkflowId}:{item.Link}";
                var payload = new FeedIngestionPayload(
                    subscriptionId,
                    wf.AutomationWorkflowId,
                    item.Title ?? "Untitled Feed Item",
                    item.Link,
                    item.Description,
                    item.PublishingDate);

                await _queue.EnqueueAsync(
                    "feed-ingestion",
                    payload,
                    EnqueueTaskOptions.WithDeduplication(deduplicationKey),
                    ct);

                queuedCount++;
            }
        }

        _logger.LogInformation("Queued {Count} feed items for processing for subscription {Title}", queuedCount, sub.Title);

        sub.LastPolledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
