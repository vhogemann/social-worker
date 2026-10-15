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
    private const string RedditUserAgent = "social-worker/1.0 (feed-reader; contact: admin@social-worker.local)";
    private readonly Services.IRssHubRouteResolver _routeResolver;
    private readonly Services.RssHubOptions _options;
    private readonly SocialWorker.Api.Features.Sources.Instagram.IInstagramDiscoveryClient? _instagramClient;
    private readonly SocialWorker.Api.Features.Sources.Instagram.IInstagramCredentialProvider? _instagramCredentialProvider;
    private readonly Microsoft.Extensions.Options.IOptions<SocialWorker.Api.Features.Sources.Instagram.InstagramOptions>? _instagramOptions;
    private readonly ILogger<DefaultFeedReaderService>? _logger;

    public DefaultFeedReaderService()
        : this(null, null, null, null, null, null)
    {
    }

    public DefaultFeedReaderService(
        Services.IRssHubRouteResolver? routeResolver,
        SocialWorker.Api.Features.Sources.Instagram.IInstagramDiscoveryClient? instagramClient = null,
        SocialWorker.Api.Features.Sources.Instagram.IInstagramCredentialProvider? instagramCredentialProvider = null,
        Microsoft.Extensions.Options.IOptions<SocialWorker.Api.Features.Sources.Instagram.InstagramOptions>? instagramOptions = null,
        ILogger<DefaultFeedReaderService>? logger = null,
        Microsoft.Extensions.Options.IOptions<Services.RssHubOptions>? options = null)
    {
        _routeResolver = routeResolver ?? new Services.RssHubRouteResolver(options, instagramOptions);
        _options = options?.Value ?? new Services.RssHubOptions();
        _instagramClient = instagramClient;
        _instagramCredentialProvider = instagramCredentialProvider;
        _instagramOptions = instagramOptions;
        _logger = logger;
    }

    public DefaultFeedReaderService(
        SocialWorker.Api.Features.Sources.Instagram.IInstagramDiscoveryClient? instagramClient,
        SocialWorker.Api.Features.Sources.Instagram.IInstagramCredentialProvider? instagramCredentialProvider,
        Microsoft.Extensions.Options.IOptions<SocialWorker.Api.Features.Sources.Instagram.InstagramOptions>? instagramOptions = null,
        ILogger<DefaultFeedReaderService>? logger = null)
        : this(new Services.RssHubRouteResolver(instagramOptions: instagramOptions), instagramClient, instagramCredentialProvider, instagramOptions, logger)
    {
    }

    public async Task<Feed> ReadFeedAsync(string feedUrl, CancellationToken ct = default)
    {
        var preferRssHub = _options.PreferRssHub;
        if (_instagramOptions?.Value != null && !_instagramOptions.Value.PreferRssHub)
        {
            preferRssHub = false;
        }

        if (_routeResolver.TryResolve(feedUrl, out var match))
        {
            if (string.Equals(match.Platform, "Reddit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(match.Platform, "RedditUser", StringComparison.OrdinalIgnoreCase))
            {
                var webUrl = !string.IsNullOrWhiteSpace(match.WebUrl) ? match.WebUrl : feedUrl;
                var redditRssUrl = $"{webUrl.TrimEnd('/')}/.rss";
                return await FeedReader.ReadAsync(redditRssUrl, ct, autoRedirect: true, userAgent: RedditUserAgent);
            }

            if (preferRssHub)
            {
                var rssUrl = $"{_routeResolver.GetBaseUrl()}{match.RoutePath}";
                try
                {
                    return await FeedReader.ReadAsync(rssUrl, ct, autoRedirect: true, userAgent: BrowserUserAgent);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to fetch {Platform} feed from RSSHub ({RssUrl}). Attempting fallback if available.", match.Platform, rssUrl);
                }
            }

            if (string.Equals(match.Platform, "Instagram", StringComparison.OrdinalIgnoreCase))
            {
                if (_instagramClient == null || _instagramCredentialProvider == null)
                {
                    throw new InvalidOperationException("Instagram discovery client or credential provider is not configured.");
                }

                var creds = await _instagramCredentialProvider.GetCredentialsAsync(null, ct);
                if (creds == null)
                {
                    throw new InvalidOperationException("Instagram polling requires a connected Instagram Professional account or configured credentials. Please connect your account in Settings or set INSTAGRAM__ACCESSTOKEN.");
                }

                var username = match.CanonicalFeedUrl.Replace("instagram://", "", StringComparison.OrdinalIgnoreCase);
                return await _instagramClient.GetProfileFeedAsync(username, creds.CallerIgUserId, creds.AccessToken, 10, ct);
            }
        }

        if (feedUrl.StartsWith("instagram://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid Instagram feed URL: {feedUrl}");
        }

        var effectiveUserAgent = feedUrl.Contains("reddit.com", StringComparison.OrdinalIgnoreCase) ? RedditUserAgent : BrowserUserAgent;
        return await FeedReader.ReadAsync(feedUrl, ct, autoRedirect: true, userAgent: effectiveUserAgent);
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

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                var contentToExtract = !string.IsNullOrWhiteSpace(item.Content) ? item.Content : item.Description;
                if (!string.IsNullOrWhiteSpace(contentToExtract))
                {
                    var (extractedUrl, extractedType) = FeedMediaExtractor.ExtractMedia(contentToExtract);
                    if (!string.IsNullOrWhiteSpace(extractedUrl))
                    {
                        mediaUrl = extractedUrl;
                        mediaType = extractedType;
                    }
                }
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

