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

        string? summary = null;
        if (sourceKind != SourceKind.YouTube && _summarizer != null && !string.IsNullOrWhiteSpace(sourceContent))
        {
            summary = await _summarizer.SummarizeAsync(sourceContent, ct);
        }

        var source = new Source
        {
            Kind = sourceKind,
            Reference = normalizedReference,
            Title = sourceTitle ?? normalizedReference,
            Content = sourceContent,
            Summary = summary,
            YoutubeVideoId = _youTubeSourceService.TryExtractYouTubeVideoId(normalizedReference),
            ProcessingStatus = sourceKind == SourceKind.YouTube ? SourceProcessingStatus.Pending : SourceProcessingStatus.Complete,
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
            _youTubeSourceService.QueueTranscriptExtraction(source.Id, draft.Id);
        }

        return new AddUrlSourceResult(source.Id, source.Reference, source.Title, source.Kind.ToString());
    }
}
