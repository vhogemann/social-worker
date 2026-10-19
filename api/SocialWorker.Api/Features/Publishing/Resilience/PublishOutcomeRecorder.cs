using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Publishing.Resilience;

public class PublishOutcomeRecorder
{
    private readonly AppDbContext _db;

    public PublishOutcomeRecorder(AppDbContext db)
    {
        _db = db;
    }

    public async Task RecordOutcomeAsync(
        PlatformThread thread,
        PublishResult result,
        CancellationToken ct = default)
    {
        var existingPosts = await _db.Posts
            .Where(p => p.PlatformThreadId == thread.Id)
            .ToListAsync(ct);

        var existingIndices = existingPosts.Select(p => p.SegmentIndex).ToHashSet();

        var userId = thread.Draft?.UserId ?? await _db.Drafts
            .Where(d => d.Id == thread.DraftId)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync(ct);

        var authorHandle = thread.Account?.Handle;
        if (string.IsNullOrEmpty(authorHandle) && thread.AccountId.HasValue)
        {
            authorHandle = await _db.Accounts
                .Where(a => a.Id == thread.AccountId.Value)
                .Select(a => a.Handle)
                .FirstOrDefaultAsync(ct);
        }

        foreach (var p in result.Posts)
        {
            if (!existingIndices.Contains(p.SegmentIndex))
            {
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DraftId = thread.DraftId,
                    PlatformThreadId = thread.Id,
                    SegmentIndex = p.SegmentIndex,
                    Platform = thread.Platform,
                    RemoteId = p.RemoteId,
                    RemoteCid = p.RemoteCid,
                    PublishedText = p.PublishedText,
                    AuthorHandle = authorHandle,
                    Url = p.Url,
                    PostedAt = DateTime.UtcNow
                };
                _db.Posts.Add(post);
                existingIndices.Add(p.SegmentIndex);
            }
        }

        if (result.Success)
        {
            thread.Stage = PlatformThreadStage.Sent;
            thread.LastPublishError = null;
        }
        else if (result.IsPartial || existingIndices.Count > 0)
        {
            thread.Stage = PlatformThreadStage.PartiallySent;
            thread.LastPublishError = result.ErrorMessage;
        }
        else
        {
            thread.LastPublishError = result.ErrorMessage;
        }

        thread.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
