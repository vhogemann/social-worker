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

        foreach (var p in result.Posts)
        {
            if (!existingIndices.Contains(p.SegmentIndex))
            {
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    DraftId = thread.DraftId,
                    PlatformThreadId = thread.Id,
                    SegmentIndex = p.SegmentIndex,
                    Platform = thread.Platform,
                    RemoteId = p.RemoteId,
                    RemoteCid = p.RemoteCid,
                    PublishedText = p.PublishedText,
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
