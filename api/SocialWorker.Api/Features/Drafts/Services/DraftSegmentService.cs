using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Drafts;

public sealed record SegmentMediaAnalysis(Guid[] ImageIds, string? YouTubeUrl, bool HasConflict);

public sealed class DraftSegmentService
{
    private readonly AppDbContext _db;

    public DraftSegmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task ReconcileSegmentsAsync(Draft draft, string markdown, CancellationToken ct = default)
    {
        var doc = ParseThread(markdown);
        var existing = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        int max = Math.Max(doc.Segments.Count, existing.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < doc.Segments.Count)
            {
                var content = doc.Segments[i].RawMarkdown;
                if (i < existing.Count)
                {
                    existing[i].Content = content;
                }
                else
                {
                    _db.ThreadSegments.Add(new ThreadSegment
                    {
                        DraftId = draft.Id,
                        Position = i,
                        Content = content
                    });
                }
            }
            else
            {
                _db.ThreadSegments.Remove(existing[i]);
            }
        }
    }

    public static ThreadMarkdownDocument ParseThread(string markdown)
    {
        return ThreadMarkdownDocument.Parse(markdown);
    }

    public static List<string> SplitMarkdownIntoSegments(string markdown)
    {
        return ParseThread(markdown).Segments.Select(s => s.RawMarkdown).ToList();
    }

    public static SegmentMediaAnalysis AnalyzeSegmentMedia(string segmentContent)
    {
        var doc = ParseThread(segmentContent);
        var segment = doc.Segments.FirstOrDefault();
        if (segment == null)
        {
            return new SegmentMediaAnalysis(Array.Empty<Guid>(), null, false);
        }

        var imageIds = segment.MediaEmbeds
            .Select(m => m.MediaId)
            .Distinct()
            .ToArray();

        var youtubeUrl = segment.YouTubeEmbed?.Url;
        bool hasConflict = imageIds.Length > 0 && youtubeUrl != null;

        return new SegmentMediaAnalysis(imageIds, youtubeUrl, hasConflict);
    }
}