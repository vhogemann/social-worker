using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Features.Sources;

public sealed record SourceDto(
    Guid Id,
    Guid DraftId,
    string Kind,
    string Reference,
    string? Title,
    string? Summary,
    string TranscriptStatus,
    string? YoutubeVideoId,
    DateTime AddedAt,
    string CanonicalUrl,
    string CitationLabel,
    string EmbedKind,
    string? CanonicalEmbedMarkdown,
    string PlainLinkLine);

public sealed record SourceDetailDto(
    Guid Id,
    Guid DraftId,
    string Kind,
    string Reference,
    string? Title,
    string? Content,
    string? Summary,
    string TranscriptStatus,
    string? YoutubeVideoId,
    DateTime AddedAt,
    string CanonicalUrl,
    string CitationLabel,
    string EmbedKind,
    string? CanonicalEmbedMarkdown,
    string PlainLinkLine);

public sealed record SourceSearchItemDto(
    Guid Id,
    string Kind,
    string Reference,
    string? Title,
    string? Summary,
    string TranscriptStatus,
    string? YoutubeVideoId,
    DateTime AddedAt,
    string CanonicalUrl,
    string CitationLabel,
    string EmbedKind,
    string? CanonicalEmbedMarkdown,
    string PlainLinkLine);

public sealed record SourceSearchResultDto(List<SourceSearchItemDto> Items, int Total, int Page, int PageSize);

public sealed record SourceStatusDto(Guid SourceId, string TranscriptStatus, string? Summary, string? YoutubeVideoId);

public sealed record AddFileSourceResult(Guid SourceId, string Reference);
public sealed record AddUrlSourceResult(Guid SourceId, string Reference, string? Title, string Kind);

public sealed class SourcesService
{
    private readonly AppDbContext _db;
    private readonly WebScraperService _scraper;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly BackgroundJobQueue? _queue;
    private SourceReconciliationService? _sourceReconciliationService;
    private SourceTranscriptionService? _sourceTranscriptionService;
    private SourceSearchService? _sourceSearchService;

    private SourceReconciliationService SourceReconciliationService =>
        _sourceReconciliationService ??= new SourceReconciliationService(_db, _scopeFactory, _queue);

    private SourceTranscriptionService SourceTranscriptionService =>
        _sourceTranscriptionService ??= new SourceTranscriptionService(_scopeFactory, _queue);

    private SourceSearchService SourceSearchService =>
        _sourceSearchService ??= new SourceSearchService(_db);

    public SourcesService(
        AppDbContext db,
        WebScraperService scraper,
        IServiceScopeFactory? scopeFactory,
        BackgroundJobQueue? queue,
        SourceReconciliationService? sourceReconciliationService = null,
        SourceTranscriptionService? sourceTranscriptionService = null,
        SourceSearchService? sourceSearchService = null)
    {
        _db = db;
        _scraper = scraper;
        _scopeFactory = scopeFactory;
        _queue = queue;
        _sourceReconciliationService = sourceReconciliationService;
        _sourceTranscriptionService = sourceTranscriptionService;
        _sourceSearchService = sourceSearchService;
    }

    public async Task<List<SourceDto>> GetSourcesForDraftAsync(Guid userId, Guid draftId, CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        var rows = await _db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.DraftId == draftId))
            .ToListAsync(ct);

        return rows.Select(s =>
        {
            var links = SourceLinkFields.Build(s.Id, s.Kind, s.Reference, s.Title);
            return new SourceDto(
                s.Id,
                draftId,
                s.Kind.ToString(),
                s.Reference,
                s.Title,
                s.Summary,
                s.TranscriptStatus.ToString(),
                s.YoutubeVideoId,
                s.AddedAt,
                links.CanonicalUrl,
                links.CitationLabel,
                links.EmbedKind,
                links.CanonicalEmbedMarkdown,
                links.PlainLinkLine);
        }).ToList();
    }

    public async Task<SourceDetailDto> GetSourceDetailAsync(Guid userId, Guid draftId, Guid sourceId, CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        var source = await _db.Sources
            .FirstOrDefaultAsync(s => s.Id == sourceId && s.DraftSources.Any(ds => ds.DraftId == draftId), ct);
        if (source == null)
        {
            throw new KeyNotFoundException("Source not found.");
        }

        var detailLinks = SourceLinkFields.Build(source.Id, source.Kind, source.Reference, source.Title);
        return new SourceDetailDto(
            source.Id,
            draftId,
            source.Kind.ToString(),
            source.Reference,
            source.Title,
            source.Content,
            source.Summary,
            source.TranscriptStatus.ToString(),
            source.YoutubeVideoId,
            source.AddedAt,
            detailLinks.CanonicalUrl,
            detailLinks.CitationLabel,
            detailLinks.EmbedKind,
            detailLinks.CanonicalEmbedMarkdown,
            detailLinks.PlainLinkLine);
    }

    public async Task<SourceSearchResultDto> SearchSourcesAsync(Guid userId, string query, int page, int pageSize, CancellationToken ct)
    {
        return await SourceSearchService.SearchSourcesAsync(userId, query, page, pageSize, ct);
    }

    public async Task<SourceDto> LinkSourceAsync(Guid userId, Guid sourceId, Guid draftId, CancellationToken ct)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        var source = await _db.Sources.FirstOrDefaultAsync(s =>
            s.Id == sourceId &&
            s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted), ct)
            ?? throw new KeyNotFoundException("Source not found or access denied.");

        var exists = await _db.DraftSources.AnyAsync(ds => ds.DraftId == draftId && ds.SourceId == sourceId, ct);
        if (!exists)
        {
            _db.DraftSources.Add(new DraftSource
            {
                DraftId = draftId,
                SourceId = sourceId,
                LinkedAt = DateTime.UtcNow
            });
            draft.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var linkFields = SourceLinkFields.Build(source.Id, source.Kind, source.Reference, source.Title);
        return new SourceDto(
            source.Id,
            draftId,
            source.Kind.ToString(),
            source.Reference,
            source.Title,
            source.Summary,
            source.TranscriptStatus.ToString(),
            source.YoutubeVideoId,
            source.AddedAt,
            linkFields.CanonicalUrl,
            linkFields.CitationLabel,
            linkFields.EmbedKind,
            linkFields.CanonicalEmbedMarkdown,
            linkFields.PlainLinkLine);
    }

    public async Task<SourceStatusDto> GetSourceStatusAsync(Guid userId, Guid sourceId, CancellationToken ct)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s =>
            s.Id == sourceId &&
            s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted), ct)
            ?? throw new KeyNotFoundException("Source not found or access denied.");

        return new SourceStatusDto(source.Id, source.TranscriptStatus.ToString(), source.Summary, source.YoutubeVideoId);
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

        SourceTranscriptionService.QueueTranscriptExtraction(source.Id, draftId.Value);

        return new SourceStatusDto(source.Id, source.TranscriptStatus.ToString(), source.Summary, source.YoutubeVideoId);
    }

    public async Task<AddFileSourceResult> AddFileSourceAsync(
        Guid userId,
        Guid draftId,
        string fileName,
        Stream fileStream,
        SourceExtractor extractor,
        CancellationToken ct)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes;
        using (var tempStream = new MemoryStream())
        {
            await fileStream.CopyToAsync(tempStream, ct);
            tempStream.Position = 0;
            hashBytes = sha256.ComputeHash(tempStream);
        }
        var shaHashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        var existing = await _db.Sources.FirstOrDefaultAsync(s => s.Sha256 == shaHashStr, ct);
        if (existing != null)
        {
            var hasLink = await _db.DraftSources.AnyAsync(ds => ds.DraftId == draftId && ds.SourceId == existing.Id, ct);
            if (!hasLink)
            {
                _db.DraftSources.Add(new DraftSource
                {
                    DraftId = draftId,
                    SourceId = existing.Id,
                    LinkedAt = DateTime.UtcNow
                });
            }
            draft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);

            return new AddFileSourceResult(existing.Id, existing.Reference);
        }

        string extractedText;
        using (var tempStreamForExtract = new MemoryStream())
        {
            fileStream.Position = 0;
            await fileStream.CopyToAsync(tempStreamForExtract, ct);
            tempStreamForExtract.Position = 0;
            extractedText = await extractor.ExtractTextAsync(fileName, tempStreamForExtract);
        }

        var newSource = new Source
        {
            Kind = SourceKind.File,
            Reference = fileName,
            Title = fileName,
            Content = extractedText,
            Sha256 = shaHashStr,
            AddedAt = DateTime.UtcNow
        };

        _db.Sources.Add(newSource);
        _db.DraftSources.Add(new DraftSource
        {
            Draft = draft,
            Source = newSource,
            LinkedAt = DateTime.UtcNow
        });
        draft.Status = DraftStatus.Editing;
        await _db.SaveChangesAsync(ct);

        return new AddFileSourceResult(newSource.Id, newSource.Reference);
    }

    public async Task ReconcileSourcesAsync(Draft draft, string content)
    {
        await SourceReconciliationService.ReconcileSourcesAsync(draft, content);
    }

    public async Task DeleteSourceAsync(Guid userId, Guid draftId, Guid sourceId, CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        var link = await _db.DraftSources.FirstOrDefaultAsync(ds => ds.DraftId == draftId && ds.SourceId == sourceId, ct);
        if (link == null)
        {
            throw new KeyNotFoundException("Source not found.");
        }

        _db.DraftSources.Remove(link);

        var hasOtherLinks = await _db.DraftSources.AnyAsync(ds => ds.SourceId == sourceId && ds.DraftId != draftId, ct);
        if (!hasOtherLinks)
        {
            var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
            if (source != null)
            {
                _db.Sources.Remove(source);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AddUrlSourceResult> AddUrlSourceAsync(
        Guid userId,
        Guid draftId,
        string reference,
        string? title,
        string? content,
        CancellationToken ct)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        var normalizedReference = reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedReference))
        {
            throw new ArgumentException("Source reference is required.", nameof(reference));
        }

        if (!TryValidateAbsoluteHttpUrl(normalizedReference, out var urlError))
        {
            throw new ArgumentException(urlError, nameof(reference));
        }

        var sourceKind = SourceKind.Url;
        var sourceTitle = title;
        var sourceContent = content;

        if (string.IsNullOrWhiteSpace(sourceContent))
        {
            var scrape = await _scraper.ScrapeUrlAsync(normalizedReference);
            if (!scrape.Success)
            {
                throw new InvalidOperationException($"Failed to scrape URL. {scrape.Error}");
            }

            normalizedReference = scrape.FinalUrl;
            sourceTitle ??= scrape.Title;
            sourceContent = scrape.Content;
            sourceKind = scrape.IsYouTube ? SourceKind.YouTube : SourceKind.Url;
        }

        var source = new Source
        {
            Kind = sourceKind,
            Reference = normalizedReference,
            Title = sourceTitle ?? normalizedReference,
            Content = sourceContent,
            YoutubeVideoId = TryExtractYouTubeVideoId(normalizedReference),
            AddedAt = DateTime.UtcNow
        };

        _db.Sources.Add(source);
        _db.DraftSources.Add(new DraftSource
        {
            Draft = draft,
            Source = source,
            LinkedAt = DateTime.UtcNow
        });
        draft.Status = DraftStatus.Editing;
        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (sourceKind == SourceKind.YouTube)
        {
            SourceTranscriptionService.QueueTranscriptExtraction(source.Id, draft.Id);
        }

        return new AddUrlSourceResult(source.Id, source.Reference, source.Title, source.Kind.ToString());
    }

    private static bool TryValidateAbsoluteHttpUrl(string reference, out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            error = "Source URLs must be absolute HTTP or HTTPS URLs.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Source URLs must use http:// or https://.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "Source URLs must include a valid host.";
            return false;
        }

        return true;
    }

    private static string? TryExtractYouTubeVideoId(string reference)
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
}
