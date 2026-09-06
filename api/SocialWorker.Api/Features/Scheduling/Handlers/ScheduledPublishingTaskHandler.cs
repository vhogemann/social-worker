using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Scheduling.Models;

namespace SocialWorker.Api.Features.Scheduling.Handlers;

public sealed class ScheduledPublishingTaskHandler : BackgroundTaskHandlerBase<ScheduledPublishingPayload>
{
    public const string TaskTypeName = "scheduled-publishing";

    private readonly AppDbContext _db;
    private readonly IPublisherResolver _publisherResolver;
    private readonly BlueskyContentValidator _blueskyContentValidator;
    private readonly ILogger<ScheduledPublishingTaskHandler> _logger;

    public override string TaskType => TaskTypeName;
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    public override int DefaultMaxAttempts => 3;

    public ScheduledPublishingTaskHandler(
        AppDbContext db,
        IPublisherResolver publisherResolver,
        BlueskyContentValidator blueskyContentValidator,
        ILogger<ScheduledPublishingTaskHandler> logger)
    {
        _db = db;
        _publisherResolver = publisherResolver;
        _blueskyContentValidator = blueskyContentValidator;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        ScheduledPublishingPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var queueItem = await _db.ScheduleQueueItems
            .Include(q => q.PlatformThread)
                .ThenInclude(t => t.Draft)
            .FirstOrDefaultAsync(q => q.Id == payload.QueueItemId, ct);

        if (queueItem == null)
        {
            _logger.LogWarning("Queue item '{QueueItemId}' was not found for scheduled publishing.", payload.QueueItemId);
            return TaskResult.Fatal($"Queue item '{payload.QueueItemId}' not found.");
        }

        if (queueItem.Status == ScheduleQueueItemStatus.Paused)
        {
            _logger.LogInformation("Queue item '{QueueItemId}' is paused; skipping execution.", queueItem.Id);
            return TaskResult.Success();
        }

        if (queueItem.Status == ScheduleQueueItemStatus.Succeeded)
        {
            _logger.LogInformation("Queue item '{QueueItemId}' already succeeded; skipping duplicate execution.", queueItem.Id);
            return TaskResult.Success();
        }

        var thread = queueItem.PlatformThread;
        if (thread == null)
        {
            queueItem.Status = ScheduleQueueItemStatus.Failed;
            queueItem.LastError = "Platform thread not found.";
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal("Platform thread not found.");
        }

        if (string.Equals(thread.Platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
        {
            var validationError = _blueskyContentValidator.GetFirstPublishValidationError(thread.Content ?? string.Empty);
            if (validationError != null)
            {
                queueItem.Status = ScheduleQueueItemStatus.Failed;
                queueItem.LastError = validationError;
                await _db.SaveChangesAsync(ct);
                return TaskResult.Fatal(validationError);
            }
        }

        Account? account = null;
        if (thread.AccountId.HasValue)
        {
            account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == thread.AccountId.Value, ct);
        }
        if (account == null)
        {
            account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == thread.Draft.UserId && a.Platform == thread.Platform, ct);
        }
        if (account == null)
        {
            var err = $"No connected account found for platform: {thread.Platform}";
            queueItem.Status = ScheduleQueueItemStatus.Failed;
            queueItem.LastError = err;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(err);
        }

        var publisher = _publisherResolver.Resolve(thread.Platform);
        if (publisher == null)
        {
            var err = $"No publisher configured for platform: {thread.Platform}";
            queueItem.Status = ScheduleQueueItemStatus.Failed;
            queueItem.LastError = err;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(err);
        }

        var publishResult = await publisher.PublishAsync(thread, account, ct);

        if (publishResult.Success)
        {
            foreach (var publishedPost in publishResult.Posts)
            {
                _db.Posts.Add(new Post
                {
                    DraftId = thread.DraftId,
                    PlatformThreadId = thread.Id,
                    SegmentIndex = publishedPost.SegmentIndex,
                    Platform = thread.Platform,
                    RemoteId = publishedPost.RemoteId,
                    Url = publishedPost.Url
                });
            }

            thread.Stage = PlatformThreadStage.Sent;
            thread.UpdatedAt = DateTime.UtcNow;

            queueItem.Status = ScheduleQueueItemStatus.Succeeded;
            queueItem.LastError = null;
            queueItem.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return TaskResult.Success();
        }

        var errorMessage = publishResult.ErrorMessage ?? "Publishing failed.";
        queueItem.LastError = errorMessage;

        if (context.AttemptCount < context.MaxAttempts)
        {
            await _db.SaveChangesAsync(ct);
            var retryDelay = TimeSpan.FromMinutes(2 * Math.Max(1, context.AttemptCount));
            return TaskResult.Retry(retryDelay, errorMessage);
        }
        else
        {
            queueItem.Status = ScheduleQueueItemStatus.Failed;
            queueItem.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(errorMessage);
        }
    }
}
