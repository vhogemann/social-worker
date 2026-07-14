using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing;

public static class PublishingEndpoint
{
    private static readonly Regex UnsupportedBlueskyMarkdownRegex = new(@"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);

    public static void MapPublishingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts/{draftId:guid}/threads").RequireAuthorization();

        group.MapPost("/{threadId:guid}/publish", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IEnumerable<IPublisher> publishers,
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
            var validationError = ValidateThreadContent(thread.Content ?? "", thread.Platform);
            if (validationError != null)
            {
                return Results.BadRequest(validationError);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == thread.Platform, ct);
            if (account == null)
            {
                return Results.BadRequest($"No connected account found for platform: {thread.Platform}");
            }

            var publisher = publishers.FirstOrDefault(p => string.Equals(p.Platform, thread.Platform, StringComparison.OrdinalIgnoreCase));
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

    private static string? ValidateThreadContent(string content, string platform)
    {
        if (platform != "Bluesky")
            return null; // Only validate for Bluesky for now

        var segments = DraftsService.SplitMarkdownIntoSegments(content);
        
        foreach (var segment in segments)
        {
            var text = SharedPatterns.MediaRegex.Replace(segment, "").Trim();
            if (string.IsNullOrEmpty(text)) continue;

            int charCount = text.Length;
            if (charCount > 300)
            {
                return $"Post exceeds 300 character limit ({charCount} characters). Please shorten the content.";
            }

            int imageCount = SharedPatterns.MediaRegex.Matches(segment).Count;
            if (imageCount > 4)
            {
                return $"Post contains {imageCount} images. Bluesky allows maximum 4 images per post.";
            }

            bool hasYouTube = segment.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                              segment.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

            if (imageCount > 0 && hasYouTube)
            {
                return "Cannot mix images and YouTube embeds in a single post on Bluesky.";
            }

            if (UnsupportedBlueskyMarkdownRegex.IsMatch(segment))
            {
                return "Post contains unsupported markdown (bold/italic/heading markers). Use plain text only.";
            }
        }

        return null;
    }
}
