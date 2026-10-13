using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Scheduling.Services;

namespace SocialWorker.Api.Features.Feeds;

public class FeedAutoPublishService
{
    private readonly AppDbContext _db;
    private readonly IPublisherResolver _publisherResolver;
    private readonly ILogger<FeedAutoPublishService> _logger;
    private readonly IScheduleQueueService? _queueService;
    private readonly ScheduleSlotEvaluatorService? _evaluator;

    public FeedAutoPublishService(
        AppDbContext db,
        IPublisherResolver publisherResolver,
        ILogger<FeedAutoPublishService> logger,
        IScheduleQueueService? queueService = null,
        ScheduleSlotEvaluatorService? evaluator = null)
    {
        _db = db;
        _publisherResolver = publisherResolver;
        _logger = logger;
        _queueService = queueService;
        _evaluator = evaluator;
    }

    public virtual async Task PublishDestinationsAsync(
        Draft canonicalDraft,
        ICollection<AutomationWorkflowDestination> destinations,
        Guid userId,
        CancellationToken ct)
    {
        foreach (var dest in destinations)
        {
            var isAuto = dest.PublishingMode == WorkflowPublishingMode.Automatic || (dest.PublishingMode == WorkflowPublishingMode.Manual && dest.AutoPublish);
            var isScheduled = dest.PublishingMode == WorkflowPublishingMode.Scheduled && dest.ScheduleId.HasValue;

            if (!isAuto && !isScheduled)
            {
                continue;
            }

            try
            {
                var variant = await _db.Drafts
                    .Include(d => d.Threads)
                    .FirstOrDefaultAsync(d => d.CanonicalDraftId == canonicalDraft.Id && d.TargetPlatform.ToString() == dest.Platform, ct);

                var thread = variant?.Threads.FirstOrDefault(t => t.Platform == dest.Platform) ??
                             canonicalDraft.Threads.FirstOrDefault(t => t.Platform == dest.Platform);

                if (thread == null || string.IsNullOrWhiteSpace(thread.Content))
                {
                    continue;
                }

                if (isScheduled && _queueService != null)
                {
                    _logger.LogInformation("Enqueuing draft {DraftId} to schedule {ScheduleId} for platform {Platform}...", variant?.Id ?? canonicalDraft.Id, dest.ScheduleId!.Value, dest.Platform);
                    await _queueService.EnqueueAsync(dest.ScheduleId.Value, thread.Id, variant?.Id ?? canonicalDraft.Id, ct: ct);
                    if (_evaluator != null)
                    {
                        await _evaluator.EvaluateScheduleAsync(dest.ScheduleId.Value, ct: ct);
                    }
                    continue;
                }

                if (isAuto)
                {
                    Account? account = null;
                    if (dest.AccountId.HasValue)
                    {
                        account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == dest.AccountId.Value && a.UserId == userId, ct);
                    }
                    if (account == null && thread.AccountId.HasValue)
                    {
                        account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == thread.AccountId.Value && a.UserId == userId, ct);
                    }
                    if (account == null)
                    {
                        account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == dest.Platform, ct);
                    }
                    if (account == null)
                    {
                        _logger.LogError("AutoPublish enabled for {Platform} but no connected account found for user {UserId}", dest.Platform, userId);
                        continue;
                    }

                    var publisher = _publisherResolver.Resolve(dest.Platform);
                    if (publisher == null)
                    {
                        _logger.LogWarning("No publisher registered for platform {Platform}", dest.Platform);
                        continue;
                    }

                    _logger.LogInformation("Auto-publishing draft {DraftId} to {Platform}...", variant?.Id ?? canonicalDraft.Id, dest.Platform);
                    var publishResult = await publisher.PublishAsync(thread, account, ct);
                    if (publishResult.Success)
                    {
                        foreach (var publishedPost in publishResult.Posts)
                        {
                            var post = new Post
                            {
                                UserId = canonicalDraft.UserId,
                                DraftId = variant?.Id ?? canonicalDraft.Id,
                                PlatformThreadId = thread.Id,
                                SegmentIndex = publishedPost.SegmentIndex,
                                Platform = dest.Platform,
                                RemoteId = publishedPost.RemoteId,
                                Url = publishedPost.Url
                            };
                            _db.Posts.Add(post);
                        }
                        thread.AccountId = account.Id;
                        thread.Stage = PlatformThreadStage.Sent;
                        thread.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                        _logger.LogInformation("Successfully auto-published to {Platform}.", dest.Platform);
                    }
                    else
                    {
                        _logger.LogError("AutoPublish failed for {Platform}: {Error}", dest.Platform, publishResult.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing destination for platform {Platform}", dest.Platform);
            }
        }
    }

    public async Task PublishIfEnabledAsync(
        Draft draft,
        bool autoPublish,
        Guid userId,
        CancellationToken ct)
    {
        if (!autoPublish)
        {
            return;
        }

        var destinations = new List<AutomationWorkflowDestination>
        {
            AutomationWorkflowDestination.Create(Guid.Empty, "Bluesky", WorkflowPublishingMode.Automatic)
        };

        await PublishDestinationsAsync(draft, destinations, userId, ct);
    }
}
