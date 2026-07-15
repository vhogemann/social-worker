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

        group.MapPost("/{threadId:guid}/publish", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IPublisherResolver publisherResolver,
            BlueskyContentValidator blueskyContentValidator,
            Guid draftId,
            Guid threadId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var thread = await db.PlatformThreads
                .Include(t => t.Draft)
                .FirstOrDefaultAsync(t => t.Id == threadId && t.DraftId == draftId && t.Draft.UserId == userId, ct);

            if (thread == null) return Results.NotFound();

            // Validate content before publishing
            var validationError = ValidateThreadContent(thread.Content ?? "", thread.Platform, blueskyContentValidator);
            if (validationError != null)
            {
                return Results.BadRequest(validationError);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == thread.Platform, ct);
            if (account == null)
            {
                return Results.BadRequest($"No connected account found for platform: {thread.Platform}");
            }

            var publisher = publisherResolver.Resolve(thread.Platform);
            if (publisher == null)
            {
                return Results.BadRequest($"No publisher configured for platform: {thread.Platform}");
            }

            var result = await publisher.PublishAsync(thread, account, ct);

            if (result.Success)
            {
                foreach (var publishedPost in result.Posts)
                {
                    var post = new Post
                    {
                        DraftId = draftId,
                        PlatformThreadId = threadId,
                        SegmentIndex = publishedPost.SegmentIndex,
                        Platform = thread.Platform,
                        RemoteId = publishedPost.RemoteId,
                        Url = publishedPost.Url
                    };
                    db.Posts.Add(post);
                }
                
                thread.Stage = PlatformThreadStage.Sent;
                thread.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(result);
        });
    }

    private static string? ValidateThreadContent(string content, string platform, BlueskyContentValidator blueskyContentValidator)
    {
        if (!string.Equals(platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
            return null; // Only validate for Bluesky for now

        return blueskyContentValidator.GetFirstPublishValidationError(content);
    }
}
