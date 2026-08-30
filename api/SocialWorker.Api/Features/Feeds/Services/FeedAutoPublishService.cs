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

    public virtual async Task PublishDestinationsAsync(
        Draft canonicalDraft,
        ICollection<AutomationWorkflowDestination> destinations,
        Guid userId,
        CancellationToken ct)
    {
        var enabledDestinations = destinations.Where(d => d.AutoPublish).ToList();
        if (enabledDestinations.Count == 0)
        {
            return;
        }

        foreach (var dest in enabledDestinations)
        {
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
                            DraftId = variant?.Id ?? canonicalDraft.Id,
                            PlatformThreadId = thread.Id,
                            SegmentIndex = publishedPost.SegmentIndex,
                            Platform = dest.Platform,
                            RemoteId = publishedPost.RemoteId,
                            Url = publishedPost.Url
                        };
                        _db.Posts.Add(post);
                    }
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error auto-publishing to {Platform}", dest.Platform);
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
            new AutomationWorkflowDestination { Platform = "Bluesky", AutoPublish = true }
        };

        await PublishDestinationsAsync(draft, destinations, userId, ct);
    }
}
