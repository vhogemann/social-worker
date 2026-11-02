using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Drafts.Services;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Models;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Publishing.Handlers;

public sealed class PublishThreadTaskHandler : BackgroundTaskHandlerBase<PublishThreadTaskPayload>
{
    public const string TaskTypeName = "publish-thread";

    private readonly AppDbContext _db;
    private readonly IPublisherResolver _publisherResolver;
    private readonly PublishValidationService _validationService;
    private readonly PublishOutcomeRecorder _outcomeRecorder;
    private readonly IDraftAutoRepairService? _repairService;
    private readonly IPublishLockService? _publishLockService;
    private readonly IBlueskyReplyTargetResolver? _replyTargetResolver;
    private readonly ILogger<PublishThreadTaskHandler> _logger;

    public override string TaskType => TaskTypeName;
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(10);
    public override int DefaultMaxAttempts => 3;

    public PublishThreadTaskHandler(
        AppDbContext db,
        IPublisherResolver publisherResolver,
        PublishValidationService validationService,
        PublishOutcomeRecorder outcomeRecorder,
        ILogger<PublishThreadTaskHandler> logger,
        IDraftAutoRepairService? repairService = null,
        IPublishLockService? publishLockService = null,
        IBlueskyReplyTargetResolver? replyTargetResolver = null)
    {
        _db = db;
        _publisherResolver = publisherResolver;
        _validationService = validationService;
        _outcomeRecorder = outcomeRecorder;
        _logger = logger;
        _repairService = repairService;
        _publishLockService = publishLockService;
        _replyTargetResolver = replyTargetResolver;
    }

    public override async Task<TaskResult> ExecuteAsync(
        PublishThreadTaskPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var thread = await _db.PlatformThreads
            .Include(t => t.Draft)
            .FirstOrDefaultAsync(t => t.Id == payload.ThreadId && t.DraftId == payload.DraftId, ct);

        if (thread == null)
        {
            _logger.LogWarning("Platform thread '{ThreadId}' not found for draft '{DraftId}'.", payload.ThreadId, payload.DraftId);
            return TaskResult.Fatal($"Platform thread '{payload.ThreadId}' not found.");
        }

        if (thread.Stage == PlatformThreadStage.Sent)
        {
            _logger.LogInformation("Thread '{ThreadId}' is already sent; skipping execution.", thread.Id);
            return TaskResult.Success();
        }

        IDisposable? lockHandle = null;
        if (_publishLockService != null)
        {
            lockHandle = await _publishLockService.TryAcquireLockAsync(thread.Id, TimeSpan.FromSeconds(30), ct);
            if (lockHandle == null)
            {
                _logger.LogWarning("Thread '{ThreadId}' is locked by another publishing operation. Retrying later.", thread.Id);
                return TaskResult.Retry(TimeSpan.FromSeconds(10), "Thread is locked by another publishing operation.");
            }

            await _db.Entry(thread).ReloadAsync(ct);
            if (thread.Stage == PlatformThreadStage.Sent)
            {
                lockHandle.Dispose();
                return TaskResult.Success();
            }
        }

        using var _ = lockHandle;

        var normalizedPlatform = SocialPlatformParser.TryParse(thread.Platform, out var parsedPlatform)
            ? parsedPlatform.ToString()
            : thread.Platform;

        Account? account = null;
        var requestedAccountId = payload.AccountId ?? thread.AccountId;
        if (requestedAccountId.HasValue)
        {
            account = await _db.Accounts.FirstOrDefaultAsync(
                a => a.Id == requestedAccountId.Value && a.UserId == payload.UserId &&
                    (a.Platform.ToLower() == thread.Platform.ToLower() || a.Platform.ToLower() == normalizedPlatform.ToLower()),
                ct);
        }

        if (account == null)
        {
            var matchingAccounts = await _db.Accounts
                .Where(a => a.UserId == payload.UserId &&
                    (a.Platform.ToLower() == thread.Platform.ToLower() || a.Platform.ToLower() == normalizedPlatform.ToLower()))
                .ToListAsync(ct);

            if (matchingAccounts.Count == 1)
            {
                account = matchingAccounts[0];
            }
            else if (matchingAccounts.Count > 1)
            {
                var err = $"Multiple accounts found for platform '{thread.Platform}'. Please specify 'accountId'.";
                thread.LastPublishError = err;
                await _db.SaveChangesAsync(ct);
                return TaskResult.Fatal(err);
            }
        }

        if (account == null)
        {
            var err = $"No connected account found for platform: {thread.Platform}";
            thread.LastPublishError = err;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(err);
        }

        thread.AccountId = account.Id;

        var validationError = await _validationService.ValidateForPublishAsync(thread, ct);
        if (validationError != null && _repairService != null)
        {
            _logger.LogWarning("Thread {ThreadId} failed validation for platform {Platform}: {Error}. Attempting agent self-repair.",
                thread.Id, thread.Platform, validationError);

            var repairResult = await _repairService.RepairPlatformThreadAsync(
                thread,
                validationError,
                thread.Draft?.UserId ?? payload.UserId,
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
            thread.LastPublishError = validationError;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(validationError);
        }

        if (_replyTargetResolver != null && string.Equals(thread.Platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = await _db.DraftBlueskyMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.DraftId == thread.DraftId, ct);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.ReplyParentUrl))
            {
                var resolved = await _replyTargetResolver.ResolveAsync(metadata.ReplyParentUrl, ct);
                if (!resolved.Success)
                {
                    var err = $"Draft reply target could not be revalidated: {resolved.Error ?? "unknown error"}";
                    thread.LastPublishError = err;
                    await _db.SaveChangesAsync(ct);
                    return TaskResult.Fatal(err);
                }

                if (!string.Equals(resolved.ReplyRootUri, metadata.ReplyRootUri, StringComparison.Ordinal)
                    || !string.Equals(resolved.ReplyRootCid, metadata.ReplyRootCid, StringComparison.Ordinal)
                    || !string.Equals(resolved.ReplyParentUri, metadata.ReplyParentUri, StringComparison.Ordinal)
                    || !string.Equals(resolved.ReplyParentCid, metadata.ReplyParentCid, StringComparison.Ordinal))
                {
                    var err = "Draft reply target metadata no longer matches the referenced Bluesky post. Please create a new draft reply target.";
                    thread.LastPublishError = err;
                    await _db.SaveChangesAsync(ct);
                    return TaskResult.Fatal(err);
                }
            }
        }

        var publisher = _publisherResolver.Resolve(normalizedPlatform) ?? _publisherResolver.Resolve(thread.Platform);
        if (publisher == null)
        {
            var err = $"No publisher configured for platform: {thread.Platform}";
            thread.LastPublishError = err;
            await _db.SaveChangesAsync(ct);
            return TaskResult.Fatal(err);
        }

        var publishResult = await publisher.PublishAsync(thread, account, ct);
        await _outcomeRecorder.RecordOutcomeAsync(thread, publishResult, ct);

        if (publishResult.Success)
        {
            return TaskResult.Success();
        }

        var errorMessage = publishResult.ErrorMessage ?? "Publishing failed.";
        if (context.AttemptCount < context.MaxAttempts)
        {
            var retryDelay = TimeSpan.FromMinutes(2 * Math.Max(1, context.AttemptCount));
            return TaskResult.Retry(retryDelay, errorMessage);
        }
        else
        {
            return TaskResult.Fatal(errorMessage);
        }
    }
}
