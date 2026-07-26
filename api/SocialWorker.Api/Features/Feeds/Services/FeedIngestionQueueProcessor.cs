using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Feeds;

public class FeedIngestionQueueProcessor
{
    private readonly AppDbContext _db;
    private readonly FeedOrchestrationService _orchestrator;
    private readonly ILogger<FeedIngestionQueueProcessor> _logger;

    public FeedIngestionQueueProcessor(
        AppDbContext db,
        FeedOrchestrationService orchestrator,
        ILogger<FeedIngestionQueueProcessor> logger)
    {
        _db = db;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task ProcessNextQueueItemAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var queueItem = await _db.FeedIngestionQueueItems
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
        await _db.SaveChangesAsync(ct);

        try
        {
            await _orchestrator.ProcessFeedItemAsync(
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
            await _db.SaveChangesAsync(ct);

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

            await _db.SaveChangesAsync(CancellationToken.None);

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
