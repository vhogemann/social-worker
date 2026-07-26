using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing;

namespace SocialWorker.Api.Features.Feeds;

public class FeedAutoPublishService
{
    private readonly AppDbContext _db;
    private readonly IPublisherResolver _publisherResolver;
    private readonly ILogger<FeedAutoPublishService> _logger;

    public FeedAutoPublishService(
        AppDbContext db,
        IPublisherResolver publisherResolver,
        ILogger<FeedAutoPublishService> logger)
    {
        _db = db;
        _publisherResolver = publisherResolver;
        _logger = logger;
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

        var finalDraft = await _db.Drafts
            .Include(d => d.Threads)
            .FirstOrDefaultAsync(d => d.Id == draft.Id, ct);

        if (finalDraft == null)
        {
            return;
        }

        var blueskyThread = finalDraft.Threads.FirstOrDefault(t => t.Platform == "Bluesky");
        if (blueskyThread == null || string.IsNullOrWhiteSpace(blueskyThread.Content))
        {
            return;
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == "Bluesky", ct);
        if (account == null)
        {
            _logger.LogError("AutoPublish enabled for feed subscription but no Bluesky account was found for user {UserId}", userId);
            return;
        }

        var publisher = _publisherResolver.Resolve("Bluesky");
        if (publisher == null)
        {
            return;
        }

        _logger.LogInformation("Auto-publishing draft {DraftId} to Bluesky...", finalDraft.Id);
        var publishResult = await publisher.PublishAsync(blueskyThread, account, ct);
        if (publishResult.Success)
        {
            foreach (var publishedPost in publishResult.Posts)
            {
                var post = new Post
                {
                    DraftId = finalDraft.Id,
                    PlatformThreadId = blueskyThread.Id,
                    SegmentIndex = publishedPost.SegmentIndex,
                    Platform = "Bluesky",
                    RemoteId = publishedPost.RemoteId,
                    Url = publishedPost.Url
                };
                _db.Posts.Add(post);
            }
            blueskyThread.Stage = PlatformThreadStage.Sent;
            blueskyThread.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully auto-published draft {DraftId}.", finalDraft.Id);
        }
        else
        {
            _logger.LogError("AutoPublish failed for draft {DraftId}: {Error}", finalDraft.Id, publishResult.ErrorMessage);
        }
    }
}
