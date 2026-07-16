using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CodeHollow.FeedReader;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedPollingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundJobQueue _queue;
    private readonly ILogger<FeedPollingHostedService> _logger;
    private readonly TimeSpan _pollingInterval;

    public FeedPollingHostedService(
        IServiceScopeFactory scopeFactory,
        BackgroundJobQueue queue,
        IConfiguration config,
        ILogger<FeedPollingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;

        var intervalMinutes = config.GetValue<double>("Feeds:PollingIntervalMinutes", 30);
        _pollingInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feed polling hosted service starting with interval: {Interval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during feed polling run.");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task PollAllSubscriptionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscriptions = await db.FeedSubscriptions.ToListAsync(ct);
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sub = await db.FeedSubscriptions.FindAsync(new object[] { subscriptionId }, ct);
        if (sub == null) return;

        _logger.LogInformation("Polling feed subscription: {Title} ({Url})", sub.Title, sub.FeedUrl);

        var feed = await FeedReader.ReadAsync(sub.FeedUrl);
        var lastPolled = sub.LastPolledAt ?? DateTime.UtcNow.AddDays(-1);

        // FeedReader parses publish dates. We filter items published since last poll.
        var newItems = feed.Items
            .Where(item => item.PublishingDate.HasValue && item.PublishingDate.Value.ToUniversalTime() > lastPolled.ToUniversalTime())
            .OrderBy(item => item.PublishingDate)
            .ToList();

        // Fallback: if LastPolledAt was null or if feed items have no publish date, we process the latest 3 items to avoid overwhelming
        if (!newItems.Any() && sub.LastPolledAt == null)
        {
            newItems = feed.Items.Take(3).ToList();
        }

        _logger.LogInformation("Found {Count} new items for subscription {Title}", newItems.Count, sub.Title);

        foreach (var item in newItems)
        {
            var itemTitle = item.Title;
            var itemLink = item.Link;
            var itemDescription = item.Description;
            var itemPubDate = item.PublishingDate;

            // Run in a separate fire-and-forget background Task to avoid blocking the sequential BackgroundJobQueue
            _ = Task.Run(async () =>
            {
                try
                {
                    using var jobScope = _scopeFactory.CreateScope();
                    var orchestrator = jobScope.ServiceProvider.GetRequiredService<FeedOrchestrationService>();
                    var jobDb = jobScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    var freshSub = await jobDb.FeedSubscriptions.FindAsync(new object[] { subscriptionId }, ct);
                    if (freshSub != null)
                    {
                        await orchestrator.ProcessFeedItemAsync(freshSub, itemTitle, itemLink, itemDescription, itemPubDate, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing feed item {Link} in background task", itemLink);
                }
            }, ct);
        }

        sub.LastPolledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
