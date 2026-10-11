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
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private readonly SocialWorker.Api.Features.Sources.Instagram.IInstagramDiscoveryClient? _instagramClient;
    private readonly SocialWorker.Api.Features.Sources.Instagram.IInstagramCredentialProvider? _instagramCredentialProvider;
    private readonly ILogger<DefaultFeedReaderService>? _logger;

    public DefaultFeedReaderService(
        SocialWorker.Api.Features.Sources.Instagram.IInstagramDiscoveryClient? instagramClient = null,
        SocialWorker.Api.Features.Sources.Instagram.IInstagramCredentialProvider? instagramCredentialProvider = null,
        ILogger<DefaultFeedReaderService>? logger = null)
    {
        _instagramClient = instagramClient;
        _instagramCredentialProvider = instagramCredentialProvider;
        _logger = logger;
    }

    public async Task<Feed> ReadFeedAsync(string feedUrl, CancellationToken ct = default)
    {
        if (SocialWorker.Api.Features.Sources.Instagram.InstagramUrlHelper.IsInstagramUrl(feedUrl))
        {
            var username = SocialWorker.Api.Features.Sources.Instagram.InstagramUrlHelper.ExtractUsername(feedUrl);
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException($"Invalid Instagram feed URL: {feedUrl}");
            }

            if (_instagramClient == null || _instagramCredentialProvider == null)
            {
                throw new InvalidOperationException("Instagram discovery client or credential provider is not configured.");
            }

            var creds = await _instagramCredentialProvider.GetCredentialsAsync(null, ct);
            if (creds == null)
            {
                throw new InvalidOperationException("Instagram polling requires a connected Instagram Professional account or configured credentials. Please connect your account in Settings or set INSTAGRAM__ACCESSTOKEN.");
            }

            return await _instagramClient.GetProfileFeedAsync(username, creds.CallerIgUserId, creds.AccessToken, 10, ct);
        }

        return await FeedReader.ReadAsync(feedUrl, ct, autoRedirect: true, userAgent: BrowserUserAgent);
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

            string? mediaUrl = null;
            string? mediaType = null;
            if (item.SpecificItem is CodeHollow.FeedReader.Feeds.Rss20FeedItem rssItem && rssItem.Enclosure != null)
            {
                mediaUrl = rssItem.Enclosure.Url;
                mediaType = rssItem.Enclosure.MediaType?.StartsWith("video", StringComparison.OrdinalIgnoreCase) == true ? "VIDEO" : rssItem.Enclosure.MediaType;
            }

            foreach (var wf in sub.WorkflowSources)
            {
                var deduplicationKey = $"feed:{subscriptionId}:{wf.AutomationWorkflowId}:{itemIdentifier}";
                var payload = new FeedIngestionPayload(
                    subscriptionId,
                    wf.AutomationWorkflowId,
                    item.Title ?? "Untitled Feed Item",
                    effectiveLink,
                    item.Description,
                    item.PublishingDate,
                    mediaUrl,
                    mediaType);

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

