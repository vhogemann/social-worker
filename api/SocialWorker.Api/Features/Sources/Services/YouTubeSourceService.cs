using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Sources;

public sealed class YouTubeSourceService : IYouTubeSourceService
{
    private readonly AppDbContext _db;
    private readonly SourceTranscriptionService _sourceTranscriptionService;

    public YouTubeSourceService(AppDbContext db, SourceTranscriptionService sourceTranscriptionService)
    {
        _db = db;
        _sourceTranscriptionService = sourceTranscriptionService;
    }

    public bool IsYouTubeUrl(string reference)
    {
        return reference.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
               reference.Contains("youtube.com/shorts/", StringComparison.OrdinalIgnoreCase) ||
               reference.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    public string? TryExtractYouTubeVideoId(string reference)
    {
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri.AbsolutePath.Trim('/');
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }

        if (!uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("live/", StringComparison.OrdinalIgnoreCase))
        {
            var pieces = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length >= 2 && !string.IsNullOrWhiteSpace(pieces[1]))
            {
                return pieces[1];
            }
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 2 && string.Equals(pieces[0], "v", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pieces[1]);
            }
        }

        return null;
    }

    public void QueueTranscriptExtraction(Guid sourceId, Guid draftId)
    {
        _sourceTranscriptionService.QueueTranscriptExtraction(sourceId, draftId);
    }

    public async Task<SourceStatusDto> RetrySourceTranscriptAsync(Guid userId, Guid sourceId, CancellationToken ct)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s =>
            s.Id == sourceId &&
            s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted), ct)
            ?? throw new KeyNotFoundException("Source not found or access denied.");

        if (source.Kind != SourceKind.YouTube)
        {
            throw new InvalidOperationException("Only YouTube sources support transcription retry.");
        }

        var draftId = await _db.DraftSources
            .Where(ds => ds.SourceId == sourceId && ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted)
            .OrderByDescending(ds => ds.LinkedAt)
            .Select(ds => (Guid?)ds.DraftId)
            .FirstOrDefaultAsync(ct);

        if (!draftId.HasValue)
        {
            throw new KeyNotFoundException("No accessible draft link found for source.");
        }

        source.TranscriptStatus = TranscriptStatus.Pending;
        source.Summary = null;
        await _db.SaveChangesAsync(ct);

        QueueTranscriptExtraction(source.Id, draftId.Value);

        return new SourceStatusDto(source.Id, source.TranscriptStatus.ToString(), source.Summary, source.YoutubeVideoId);
    }
}
