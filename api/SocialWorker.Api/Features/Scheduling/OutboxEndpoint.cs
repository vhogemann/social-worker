using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Scheduling;

public static class OutboxEndpoint
{
    public static void MapOutboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/outbox").RequireAuthorization();

        // 1. List pending outbox drafts
        group.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var drafts = await db.Drafts
                .Where(d => d.UserId == userId.Value &&
                            d.AutomationWorkflowId != null &&
                            d.Status != DraftStatus.Archived &&
                            d.Status != DraftStatus.Deleted &&
                            d.Status != DraftStatus.Discarded)
                .Include(d => d.AutomationWorkflow)
                .Include(d => d.Threads)
                    .ThenInclude(t => t.Account)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);

            // Filter to drafts that have at least one thread not yet sent or scheduled
            var pendingDrafts = drafts.Where(d => d.Threads.Any(t => t.Stage == PlatformThreadStage.Draft)).ToList();

            var dtos = pendingDrafts.Select(d => new OutboxDraftDto(
                DraftId: d.Id,
                Title: d.Title,
                Content: d.Content ?? string.Empty,
                WorkflowId: d.AutomationWorkflowId,
                WorkflowTitle: d.AutomationWorkflow?.Title,
                Threads: d.Threads.Select(t => new OutboxThreadDto(
                    ThreadId: t.Id,
                    Platform: t.Platform,
                    Stage: t.Stage,
                    Content: t.Content,
                    AccountId: t.AccountId,
                    AccountHandle: t.Account?.Handle
                )).ToList(),
                CreatedAt: d.CreatedAt,
                UpdatedAt: d.UpdatedAt
            )).ToList();

            return Results.Ok(dtos);
        });

        // 2. Approve draft: queue or publish now
        group.MapPost("/{draftId:guid}/approve", ApproveOutboxAsync);

        // 3. Dismiss outbox draft
        group.MapDelete("/{draftId:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid draftId, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId.Value, ct);
            if (draft == null)
                return Results.NotFound(new ApiErrorResponse("Draft not found."));

            draft.Status = DraftStatus.Archived;
            draft.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });
    }

    public static async Task<IResult> ApproveOutboxAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        IScheduleQueueService queueService,
        ScheduleSlotEvaluatorService evaluator,
        IPublisherResolver publisherResolver,
        PublishValidationService validationService,
        PublishOutcomeRecorder outcomeRecorder,
        IPublishLockService publishLockService,
        Guid draftId,
        ApproveOutboxRequest request,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var draft = await db.Drafts
            .Include(d => d.Threads)
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId.Value, ct);

        if (draft == null)
            return Results.NotFound(new ApiErrorResponse("Draft not found."));

        if (string.Equals(request.Action, "queue", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.ScheduleId.HasValue)
                return Results.BadRequest(new ApiErrorResponse("ScheduleId is required to enqueue draft."));

            var scheduleExists = await db.Schedules
                .AnyAsync(s => s.Id == request.ScheduleId.Value && s.UserId == userId.Value, ct);

            if (!scheduleExists)
                return Results.BadRequest(new ApiErrorResponse("Specified schedule was not found."));

            var targetThreads = request.PlatformThreadId.HasValue
                ? draft.Threads.Where(t => t.Id == request.PlatformThreadId.Value).ToList()
                : draft.Threads.Where(t => t.Stage == PlatformThreadStage.Draft).ToList();

            if (targetThreads.Count == 0)
                return Results.BadRequest(new ApiErrorResponse("No eligible platform threads found to enqueue."));

            foreach (var thread in targetThreads)
            {
                await queueService.EnqueueAsync(request.ScheduleId.Value, thread.Id, draft.Id, ct: ct);
            }

            await evaluator.EvaluateScheduleAsync(request.ScheduleId.Value, ct: ct);
            return Results.Ok(new { message = $"Draft threads enqueued into schedule {request.ScheduleId.Value}." });
        }
        else if (string.Equals(request.Action, "publish_now", StringComparison.OrdinalIgnoreCase))
        {
            var targetThreads = request.PlatformThreadId.HasValue
                ? draft.Threads.Where(t => t.Id == request.PlatformThreadId.Value).ToList()
                : draft.Threads.ToList();

            if (targetThreads.Count == 0)
                return Results.BadRequest(new ApiErrorResponse("No eligible platform threads found to publish."));

            if (targetThreads.All(t => t.Stage == PlatformThreadStage.Sent))
            {
                var targetIds = targetThreads.Select(t => t.Id).ToList();
                var existingCount = await db.Posts
                    .CountAsync(p => p.PlatformThreadId.HasValue && targetIds.Contains(p.PlatformThreadId.Value), ct);
                return Results.Ok(new { message = "Published successfully.", postsCount = existingCount });
            }

            var publishedPosts = new List<Post>();

            foreach (var thread in targetThreads)
            {
                using var handle = await publishLockService.TryAcquireLockAsync(thread.Id, TimeSpan.FromSeconds(30), ct);
                if (handle == null)
                {
                    return Results.Conflict(new ApiErrorResponse($"A publishing operation is currently in progress for thread {thread.Id}."));
                }

                if (thread.Stage == PlatformThreadStage.Sent)
                {
                    var existing = await db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync(ct);
                    publishedPosts.AddRange(existing);
                    continue;
                }

                Account? account = null;
                if (thread.AccountId.HasValue)
                {
                    account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == thread.AccountId.Value && a.UserId == userId.Value, ct);
                }
                if (account == null)
                {
                    account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId.Value && a.Platform == thread.Platform, ct);
                }
                if (account == null)
                {
                    return Results.BadRequest(new ApiErrorResponse($"No connected account found for platform: {thread.Platform}"));
                }

                var validationError = await validationService.ValidateForPublishAsync(thread, ct);
                if (validationError != null)
                    return Results.BadRequest(new ApiErrorResponse(validationError));

                var publisher = publisherResolver.Resolve(thread.Platform);
                if (publisher == null)
                {
                    return Results.BadRequest(new ApiErrorResponse($"No publisher configured for platform: {thread.Platform}"));
                }

                var result = await publisher.PublishAsync(thread, account, ct);
                if (!result.Success)
                {
                    return Results.BadRequest(new ApiErrorResponse(result.ErrorMessage ?? "Publishing failed."));
                }

                await outcomeRecorder.RecordOutcomeAsync(thread, result, ct);

                var posts = await db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync(ct);
                publishedPosts.AddRange(posts);
            }

            return Results.Ok(new { message = "Published successfully.", postsCount = publishedPosts.Count });
        }
        else
        {
            return Results.BadRequest(new ApiErrorResponse("Invalid action. Must be 'queue' or 'publish_now'."));
        }
    }
}
