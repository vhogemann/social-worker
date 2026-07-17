using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedIngestionQueueHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedIngestionQueueHostedService> _logger;
    private readonly TimeSpan _pollingInterval;

    public FeedIngestionQueueHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<FeedIngestionQueueHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = config.GetValue<double>("Feeds:QueuePollingIntervalSeconds", 10);
        _pollingInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feed ingestion queue hosted service starting with interval: {Interval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextQueueItemAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing feed ingestion queue.");
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

    private async Task ProcessNextQueueItemAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<FeedOrchestrationService>();

        var now = DateTime.UtcNow;

        var queueItem = await db.FeedIngestionQueueItems
            .Include(q => q.FeedSubscription)
            .Where(q => (q.Status == FeedQueueItemStatus.Pending || q.Status == FeedQueueItemStatus.Failed) &&
                        q.AttemptCount < q.MaxAttempts &&
                        q.NextAttemptAt <= now)
            .OrderBy(q => q.NextAttemptAt)
            .ThenBy(q => q.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (queueItem == null)
        {
            return;
        }

        queueItem.Status = FeedQueueItemStatus.Processing;
        queueItem.AttemptCount += 1;
        queueItem.LastAttemptAt = now;
        queueItem.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        try
        {
            await orchestrator.ProcessFeedItemAsync(
                queueItem.FeedSubscription,
                queueItem.ItemTitle,
                queueItem.ItemLink,
                queueItem.ItemDescription ?? string.Empty,
                queueItem.ItemPublishedAt,
                ct);

            queueItem.Status = FeedQueueItemStatus.Succeeded;
            queueItem.LastError = null;
            queueItem.CompletedAt = DateTime.UtcNow;
            queueItem.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully processed feed queue item {QueueItemId} ({Link})", queueItem.Id, queueItem.ItemLink);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            queueItem.Status = FeedQueueItemStatus.Failed;
            queueItem.LastError = ex.Message;
            queueItem.NextAttemptAt = DateTime.UtcNow + ComputeRetryDelay(queueItem.AttemptCount);
            queueItem.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex, "Failed to process feed queue item {QueueItemId} ({Link}), attempt {Attempt}/{MaxAttempts}",
                queueItem.Id,
                queueItem.ItemLink,
                queueItem.AttemptCount,
                queueItem.MaxAttempts);
        }
    }

    internal static TimeSpan ComputeRetryDelay(int attemptCount)
    {
        var multiplier = Math.Min(Math.Max(attemptCount, 1), 5);
        return TimeSpan.FromSeconds(30 * Math.Pow(2, multiplier - 1));
    }
}
