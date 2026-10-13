using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Sources;

public sealed class UrlSourceService : IUrlSourceService
{
    private readonly AppDbContext _db;
    private readonly WebScraperService _scraper;
    private readonly SummarizationService? _summarizer;
    private readonly ISourceUrlValidator _urlValidator;
    private readonly IYouTubeSourceService _youTubeSourceService;

    public UrlSourceService(
        AppDbContext db,
        WebScraperService scraper,
        SummarizationService? summarizer,
        ISourceUrlValidator urlValidator,
        IYouTubeSourceService youTubeSourceService)
    {
        _db = db;
        _scraper = scraper;
        _summarizer = summarizer;
        _urlValidator = urlValidator;
        _youTubeSourceService = youTubeSourceService;
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

        _urlValidator.EnsureAbsoluteHttpUrl(normalizedReference, nameof(reference));

        var initialYoutubeId = _youTubeSourceService.TryExtractYouTubeVideoId(normalizedReference);
        var existing = await _db.Sources.FirstOrDefaultAsync(s =>
            s.Reference == normalizedReference ||
            (initialYoutubeId != null && s.YoutubeVideoId == initialYoutubeId), ct);

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
            draft.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (existing.Kind == SourceKind.YouTube && existing.ProcessingStatus == SourceProcessingStatus.Failed)
            {
                _youTubeSourceService.QueueTranscriptExtraction(existing.Id, draft.Id);
            }

            return new AddUrlSourceResult(existing.Id, existing.Reference, existing.Title, existing.Kind.ToString());
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

            var postScrapeYoutubeId = _youTubeSourceService.TryExtractYouTubeVideoId(normalizedReference);
            var existingAfterScrape = await _db.Sources.FirstOrDefaultAsync(s =>
                s.Reference == normalizedReference ||
                (postScrapeYoutubeId != null && s.YoutubeVideoId == postScrapeYoutubeId), ct);

            if (existingAfterScrape != null)
            {
                var hasLink = await _db.DraftSources.AnyAsync(ds => ds.DraftId == draftId && ds.SourceId == existingAfterScrape.Id, ct);
                if (!hasLink)
                {
                    _db.DraftSources.Add(new DraftSource
                    {
                        DraftId = draftId,
                        SourceId = existingAfterScrape.Id,
                        LinkedAt = DateTime.UtcNow
                    });
                }

                draft.Status = DraftStatus.Editing;
                draft.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                return new AddUrlSourceResult(existingAfterScrape.Id, existingAfterScrape.Reference, existingAfterScrape.Title, existingAfterScrape.Kind.ToString());
            }
        }

        string? summary = null;
        if (sourceKind != SourceKind.YouTube && _summarizer != null && !string.IsNullOrWhiteSpace(sourceContent))
        {
            summary = await _summarizer.SummarizeAsync(sourceContent, ct);
        }

        var youtubeId = _youTubeSourceService.TryExtractYouTubeVideoId(normalizedReference);
        var source = sourceKind == SourceKind.YouTube
            ? Source.CreateYouTube(youtubeId ?? "pending", normalizedReference, sourceTitle ?? normalizedReference)
            : Source.CreateUrl(normalizedReference, sourceTitle ?? normalizedReference);

        source.Content = sourceContent;
        source.Summary = summary;
        if (sourceKind != SourceKind.YouTube)
        {
            source.ProcessingStatus = SourceProcessingStatus.Complete;
        }

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
            _youTubeSourceService.QueueTranscriptExtraction(source.Id, draft.Id);
        }

        return new AddUrlSourceResult(source.Id, source.Reference, source.Title, source.Kind.ToString());
    }
}
