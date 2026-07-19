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
    private readonly SourceReconciliationService _sourceReconciliationService;
    private readonly SourceTranscriptionService _sourceTranscriptionService;
    private readonly SourceSearchService _sourceSearchService;
    private readonly IUrlSourceService _urlSourceService;
    private readonly IYouTubeSourceService _youTubeSourceService;
    private readonly IFileSourceService _fileSourceService;

    private readonly SummarizationService? _summarizer;

    public SourcesService(
        AppDbContext db,
        WebScraperService scraper,
        IServiceScopeFactory? scopeFactory,
        BackgroundJobQueue? queue,
        SourceReconciliationService? sourceReconciliationService = null,
        SourceTranscriptionService? sourceTranscriptionService = null,
        SourceSearchService? sourceSearchService = null,
        IUrlSourceService? urlSourceService = null,
        IYouTubeSourceService? youTubeSourceService = null,
        IFileSourceService? fileSourceService = null,
        SummarizationService? summarizer = null)
    {
        _db = db;
        _scraper = scraper;
        _scopeFactory = scopeFactory;
        _queue = queue;
        _summarizer = summarizer;

        _sourceReconciliationService = sourceReconciliationService ?? new SourceReconciliationService(_db, _scopeFactory, _queue);
        _sourceTranscriptionService = sourceTranscriptionService ?? new SourceTranscriptionService(_scopeFactory, _queue);
        _sourceSearchService = sourceSearchService ?? new SourceSearchService(_db);
        _youTubeSourceService = youTubeSourceService ?? new YouTubeSourceService(_db, _sourceTranscriptionService);
        _urlSourceService = urlSourceService ?? new UrlSourceService(_db, _scraper, _summarizer, new SourceUrlValidator(), _youTubeSourceService);
        _fileSourceService = fileSourceService ?? new FileSourceService(_db, _summarizer);
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

    public async Task<SourceSearchResultDto> SearchSourcesAsync(
        Guid userId,
        string query,
        int page,
        int pageSize,
        CancellationToken ct,
        SourceKind? kindFilter = null,
        DateTime? addedAfter = null,
        DateTime? addedBefore = null,
        Guid? excludeDraftId = null)
    {
        return await _sourceSearchService.SearchSourcesAsync(userId, query, page, pageSize, ct, kindFilter, addedAfter, addedBefore, excludeDraftId);
    }

    public async Task<SourceDetailDto> GetSourceDetailByIdAsync(Guid userId, Guid sourceId, CancellationToken ct)
    {
        var source = await _db.Sources
            .FirstOrDefaultAsync(s =>
                s.Id == sourceId &&
                s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted), ct)
            ?? throw new KeyNotFoundException("Source not found or access denied.");

        var firstDraftId = await _db.DraftSources
            .Where(ds => ds.SourceId == sourceId && ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted)
            .OrderByDescending(ds => ds.LinkedAt)
            .Select(ds => ds.DraftId)
            .FirstOrDefaultAsync(ct);

        var detailLinks = SourceLinkFields.Build(source.Id, source.Kind, source.Reference, source.Title);
        return new SourceDetailDto(
            source.Id,
            firstDraftId,
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
        return await _youTubeSourceService.RetrySourceTranscriptAsync(userId, sourceId, ct);
    }

    public async Task<AddFileSourceResult> AddFileSourceAsync(
        Guid userId,
        Guid draftId,
        string fileName,
        Stream fileStream,
        SourceExtractor extractor,
        CancellationToken ct)
    {
        return await _fileSourceService.AddFileSourceAsync(userId, draftId, fileName, fileStream, extractor, ct);
    }

    public async Task ReconcileSourcesAsync(Draft draft, string content)
    {
        await _sourceReconciliationService.ReconcileSourcesAsync(draft, content);
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
        return await _urlSourceService.AddUrlSourceAsync(userId, draftId, reference, title, content, ct);
    }
}
