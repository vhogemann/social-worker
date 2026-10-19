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
    private readonly SocialWorker.Api.Features.Publishing.Validation.PublishValidationService _validationService;
    private readonly SocialWorker.Api.Features.Publishing.Resilience.PublishOutcomeRecorder _outcomeRecorder;
    private readonly SocialWorker.Api.Features.Drafts.Services.IDraftAutoRepairService? _repairService;
    private readonly SocialWorker.Api.Features.Publishing.Resilience.IPublishLockService? _publishLockService;
    private readonly ILogger<ScheduledPublishingTaskHandler> _logger;

    public override string TaskType => TaskTypeName;
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    public override int DefaultMaxAttempts => 3;

    public ScheduledPublishingTaskHandler(
        AppDbContext db,
        IPublisherResolver publisherResolver,
        SocialWorker.Api.Features.Publishing.Validation.PublishValidationService validationService,
        SocialWorker.Api.Features.Publishing.Resilience.PublishOutcomeRecorder outcomeRecorder,
        ILogger<ScheduledPublishingTaskHandler> logger,
        SocialWorker.Api.Features.Drafts.Services.IDraftAutoRepairService? repairService = null,
        SocialWorker.Api.Features.Publishing.Resilience.IPublishLockService? publishLockService = null)
    {
        _db = db;
        _publisherResolver = publisherResolver;
        _validationService = validationService;
        _outcomeRecorder = outcomeRecorder;
        _logger = logger;
        _repairService = repairService;
        _publishLockService = publishLockService;
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

        if (thread.Stage == PlatformThreadStage.Sent)
        {
            _logger.LogInformation("Thread '{ThreadId}' for queue item '{QueueItemId}' is already sent; marking succeeded.", thread.Id, queueItem.Id);
            queueItem.Status = ScheduleQueueItemStatus.Succeeded;
            queueItem.LastError = null;
            queueItem.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Success();
        }

        IDisposable? lockHandle = null;
        if (_publishLockService != null)
        {
            lockHandle = await _publishLockService.TryAcquireLockAsync(thread.Id, TimeSpan.FromSeconds(30), ct);
            if (lockHandle == null)
            {
                _logger.LogWarning("Thread '{ThreadId}' is locked by another publishing operation. Retrying scheduled publishing later.", thread.Id);
                return TaskResult.Retry(TimeSpan.FromSeconds(10), "Thread is locked by another publishing operation.");
            }

            await _db.Entry(thread).ReloadAsync(ct);
            if (thread.Stage == PlatformThreadStage.Sent)
            {
                lockHandle.Dispose();
                queueItem.Status = ScheduleQueueItemStatus.Succeeded;
                queueItem.LastError = null;
                queueItem.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return TaskResult.Success();
            }
        }

        using var _ = lockHandle;


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

        var validationError = await _validationService.ValidateForPublishAsync(thread, ct);
        if (validationError != null && _repairService != null)
        {
            _logger.LogWarning("Thread {ThreadId} failed validation for platform {Platform}: {Error}. Attempting agent self-repair.",
                thread.Id, thread.Platform, validationError);

            var repairResult = await _repairService.RepairPlatformThreadAsync(
                thread,
                validationError,
                thread.Draft?.UserId ?? account.UserId,
                ct);

            if (repairResult.Success)
            {
                _logger.LogInformation("Thread {ThreadId} successfully repaired by agent. Re-validating before publish.", thread.Id);
                validationError = await _validationService.ValidateForPublishAsync(thread, ct);
            }
            else
            {
                _logger.LogWarning("Agent self-repair failed for thread {ThreadId}: {RepairError}", thread.Id, repairResult.ErrorMessage);
            }
        }

        if (validationError != null)
        {
            queueItem.Status = ScheduleQueueItemStatus.Failed;
            queueItem.LastError = validationError;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(validationError);
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
        await _outcomeRecorder.RecordOutcomeAsync(thread, publishResult, ct);

        if (publishResult.Success)
        {
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
