using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Drafts;

public sealed record SegmentMediaAnalysis(Guid[] ImageIds, string? YouTubeUrl, bool HasConflict);

public sealed class DraftSegmentService
{
    private static readonly Regex YouTubeEmbedRegex = new(@"!\[.*?\]\((https?://(?:www\.)?youtube\.com/watch\?v=[\w-]+|https?://youtu\.be/[\w-]+)\)", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public DraftSegmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task ReconcileSegmentsAsync(Draft draft, string markdown, CancellationToken ct = default)
    {
        var rawSegments = SplitMarkdownIntoSegments(markdown);
        var existing = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        int max = Math.Max(rawSegments.Count, existing.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < rawSegments.Count)
            {
                var content = rawSegments[i];
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

    public static List<string> SplitMarkdownIntoSegments(string markdown)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(markdown))
        {
            return new List<string> { string.Empty };
        }

        var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var current = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Trim() == "---")
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                if (current.Length > 0)
                {
                    current.AppendLine();
                }
                current.Append(line);
            }
        }

        result.Add(current.ToString().Trim());
        return result;
    }

    public static SegmentMediaAnalysis AnalyzeSegmentMedia(string segmentContent)
    {
        var imageIds = SharedPatterns.ExtractMediaReferences(segmentContent)
            .Select(reference => reference.MediaId)
            .Distinct()
            .ToArray();

        string? youtubeUrl = null;
        var ytMatch = YouTubeEmbedRegex.Match(segmentContent);
        if (ytMatch.Success)
        {
            youtubeUrl = ytMatch.Groups[1].Value;
        }

        bool hasConflict = imageIds.Length > 0 && youtubeUrl != null;

        return new SegmentMediaAnalysis(imageIds, youtubeUrl, hasConflict);
    }
}