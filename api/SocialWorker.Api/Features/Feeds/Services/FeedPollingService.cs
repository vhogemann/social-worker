using System;
using System.Collections.Generic;
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

public interface IFeedReaderService
{
    Task<Feed> ReadFeedAsync(string feedUrl, CancellationToken ct = default);
}

public sealed class DefaultFeedReaderService : IFeedReaderService
{
    public Task<Feed> ReadFeedAsync(string feedUrl, CancellationToken ct = default)
    {
        return FeedReader.ReadAsync(feedUrl, ct);
    }
}

public sealed class FeedPollingService
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskQueue _queue;
    private readonly IFeedReaderService _feedReader;
    private readonly ILogger<FeedPollingService> _logger;

    public FeedPollingService(
        AppDbContext db,
        IBackgroundTaskQueue queue,
        ILogger<FeedPollingService> logger,
        IFeedReaderService? feedReader = null)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
        _feedReader = feedReader ?? new DefaultFeedReaderService();
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

    public async Task PollSubscriptionAsync(Guid subscriptionId, CancellationToken ct, bool forceAll = false)
    {
        var sub = await _db.FeedSubscriptions
            .Include(s => s.WorkflowSources)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (sub == null) return;

        _logger.LogInformation("Polling feed subscription: {Title} ({Url}), forceAll: {ForceAll}", sub.Title, sub.FeedUrl, forceAll);

        var feed = await _feedReader.ReadFeedAsync(sub.FeedUrl, ct);
        var lastPolled = sub.LastPolledAt;

        List<FeedItem> itemsToProcess;
        if (forceAll || lastPolled == null)
        {
            itemsToProcess = feed.Items
                .OrderBy(item => item.PublishingDate ?? DateTime.MinValue)
                .ToList();
        }
        else
        {
            itemsToProcess = feed.Items
                .Where(item => !item.PublishingDate.HasValue || item.PublishingDate.Value.ToUniversalTime() > lastPolled.Value.ToUniversalTime())
                .OrderBy(item => item.PublishingDate ?? DateTime.MinValue)
                .ToList();
        }

        _logger.LogInformation("Found {Count} items to evaluate for subscription {Title}", itemsToProcess.Count, sub.Title);

        var queuedCount = 0;

        foreach (var item in itemsToProcess)
        {
            var itemIdentifier = !string.IsNullOrWhiteSpace(item.Link)
                ? item.Link
                : (!string.IsNullOrWhiteSpace(item.Id) ? item.Id : $"{item.Title}_{item.PublishingDate:O}");

            if (string.IsNullOrWhiteSpace(itemIdentifier))
                continue;

            var effectiveLink = !string.IsNullOrWhiteSpace(item.Link) ? item.Link : itemIdentifier;

            foreach (var wf in sub.WorkflowSources)
            {
                var deduplicationKey = $"feed:{subscriptionId}:{wf.AutomationWorkflowId}:{itemIdentifier}";
                var payload = new FeedIngestionPayload(
                    subscriptionId,
                    wf.AutomationWorkflowId,
                    item.Title ?? "Untitled Feed Item",
                    effectiveLink,
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

