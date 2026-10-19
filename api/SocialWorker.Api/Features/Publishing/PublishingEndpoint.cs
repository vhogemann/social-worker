using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing;

public static class PublishingEndpoint
{
    public static void MapPublishingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts/{draftId:guid}/threads").RequireAuthorization();
        group.MapPost("/{threadId:guid}/publish", PublishThreadAsync);
    }

    public static async Task<IResult> PublishThreadAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        IPublisherResolver publisherResolver,
        Validation.PublishValidationService validationService,
        Resilience.PublishOutcomeRecorder outcomeRecorder,
        Resilience.IPublishLockService lockService,
        Guid draftId,
        Guid threadId,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        using var handle = await lockService.TryAcquireLockAsync(threadId, TimeSpan.FromSeconds(30), ct);
        if (handle == null)
        {
            return Results.Conflict(new ApiErrorResponse("A publishing operation is currently in progress for this thread."));
        }

        var thread = await db.PlatformThreads
            .Include(t => t.Draft)
            .FirstOrDefaultAsync(t => t.Id == threadId && t.DraftId == draftId && t.Draft.UserId == userId, ct);

        if (thread == null) return Results.NotFound(new ApiErrorResponse("Platform thread not found."));

        if (thread.Stage == PlatformThreadStage.Sent)
        {
            var existingPosts = await db.Posts
                .Where(p => p.PlatformThreadId == thread.Id)
                .OrderBy(p => p.SegmentIndex)
                .Select(p => new PublishedPost
                {
                    SegmentIndex = p.SegmentIndex,
                    RemoteId = p.RemoteId ?? "",
                    RemoteCid = p.RemoteCid,
                    PublishedText = p.PublishedText,
                    Url = p.Url ?? ""
                })
                .ToListAsync(ct);

            return Results.Ok(PublishResult.Succeeded(existingPosts));
        }

        var validationError = await validationService.ValidateForPublishAsync(thread, ct);
        if (validationError != null)
        {
            return Results.BadRequest(new ApiErrorResponse(validationError));
        }

        Account? account = null;
        if (thread.AccountId.HasValue)
        {
            account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == thread.AccountId.Value && a.UserId == userId, ct);
        }
        if (account == null)
        {
            account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == thread.Platform, ct);
        }
        if (account == null)
        {
            return Results.BadRequest(new ApiErrorResponse($"No connected account found for platform: {thread.Platform}"));
        }

        var publisher = publisherResolver.Resolve(thread.Platform);
        if (publisher == null)
        {
            return Results.BadRequest(new ApiErrorResponse($"No publisher configured for platform: {thread.Platform}"));
        }

        var result = await publisher.PublishAsync(thread, account, ct);

        await outcomeRecorder.RecordOutcomeAsync(thread, result, ct);

        return Results.Ok(result);
    }
}
