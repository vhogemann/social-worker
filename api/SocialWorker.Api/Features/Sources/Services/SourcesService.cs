using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    DateTime AddedAt);

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
    DateTime AddedAt);

public sealed record SourceSearchItemDto(
    Guid Id,
    string Kind,
    string Reference,
    string? Title,
    string? Summary,
    string TranscriptStatus,
    string? YoutubeVideoId,
    DateTime AddedAt);

public sealed record SourceSearchResultDto(List<SourceSearchItemDto> Items, int Total, int Page, int PageSize);

public sealed record SourceStatusDto(Guid SourceId, string TranscriptStatus, string? Summary, string? YoutubeVideoId);

public sealed record AddFileSourceResult(Guid SourceId, string Reference);
public sealed record AddUrlSourceResult(Guid SourceId, string Reference, string? Title, string Kind);

public sealed class SourcesService
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s\)\""'<>]+", RegexOptions.Compiled);
    private static readonly Regex FileRegex = new(@"file://([0-9a-fA-F\-]{36})", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly WebScraperService _scraper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundJobQueue _queue;

    public SourcesService(AppDbContext db, WebScraperService scraper, IServiceScopeFactory scopeFactory, BackgroundJobQueue queue)
    {
        _db = db;
        _scraper = scraper;
        _scopeFactory = scopeFactory;
        _queue = queue;
    }

    public async Task<List<SourceDto>> GetSourcesForDraftAsync(Guid userId, Guid draftId, CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        return await _db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.DraftId == draftId))
            .Select(s => new SourceDto(
                s.Id,
                draftId,
                s.Kind.ToString(),
                s.Reference,
                s.Title,
                s.Summary,
                s.TranscriptStatus.ToString(),
                s.YoutubeVideoId,
                s.AddedAt))
            .ToListAsync(ct);
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
            source.AddedAt);
    }

    public async Task<SourceSearchResultDto> SearchSourcesAsync(Guid userId, string query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedQuery = query?.Trim() ?? string.Empty;

        var accessibleSources = _db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted));

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            if (_db.Database.IsNpgsql())
            {
                var like = $"%{normalizedQuery}%";
                accessibleSources = accessibleSources.Where(s =>
                    EF.Functions.ILike(s.Reference, like) ||
                    (s.Title != null && EF.Functions.ILike(s.Title, like)) ||
                    (s.Content != null && EF.Functions.ILike(s.Content, like)) ||
                    (s.Summary != null && EF.Functions.ILike(s.Summary, like)));
            }
            else
            {
                var lowered = normalizedQuery.ToLower();
                accessibleSources = accessibleSources.Where(s =>
                    s.Reference.ToLower().Contains(lowered) ||
                    (s.Title != null && s.Title.ToLower().Contains(lowered)) ||
                    (s.Content != null && s.Content.ToLower().Contains(lowered)) ||
                    (s.Summary != null && s.Summary.ToLower().Contains(lowered)));
            }
        }

        var total = await accessibleSources.Select(s => s.Id).Distinct().CountAsync(ct);

        var items = await accessibleSources
            .OrderByDescending(s => s.AddedAt)
            .Select(s => new SourceSearchItemDto(
                s.Id,
                s.Kind.ToString(),
                s.Reference,
                s.Title,
                s.Summary,
                s.TranscriptStatus.ToString(),
                s.YoutubeVideoId,
                s.AddedAt))
            .Distinct()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new SourceSearchResultDto(items, total, page, pageSize);
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

        return new SourceDto(
            source.Id,
            draftId,
            source.Kind.ToString(),
            source.Reference,
            source.Title,
            source.Summary,
            source.TranscriptStatus.ToString(),
            source.YoutubeVideoId,
            source.AddedAt);
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

        QueueTranscriptExtraction(source.Id, draftId.Value);

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
        try
        {
            using (var tempStreamForExtract = new MemoryStream())
            {
                fileStream.Position = 0;
                await fileStream.CopyToAsync(tempStreamForExtract, ct);
                tempStreamForExtract.Position = 0;
                extractedText = await extractor.ExtractTextAsync(fileName, tempStreamForExtract);
            }
        }
        catch (Exception)
        {
            throw;
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
        var urls = UrlRegex.Matches(content)
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        var fileIds = FileRegex.Matches(content)
            .Select(m => Guid.TryParse(m.Groups[1].Value, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var existing = await _db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.DraftId == draft.Id))
            .ToListAsync();

        var draftLinks = await _db.DraftSources
            .Where(ds => ds.DraftId == draft.Id)
            .ToListAsync();

        bool changed = false;
        foreach (var src in existing)
        {
            if (src.Kind == SourceKind.File && !fileIds.Contains(src.Id))
            {
                var link = draftLinks.FirstOrDefault(ds => ds.SourceId == src.Id);
                if (link != null)
                {
                    _db.DraftSources.Remove(link);
                    changed = true;

                    var hasOtherLinks = await _db.DraftSources.AnyAsync(ds => ds.SourceId == src.Id && ds.DraftId != draft.Id);
                    if (!hasOtherLinks)
                    {
                        _db.Sources.Remove(src);
                    }
                }
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }

        var newUrls = urls.Where(url => !existing.Any(e => (e.Kind == SourceKind.Url || e.Kind == SourceKind.YouTube) && e.Reference == url)).ToList();
        if (newUrls.Count > 0)
        {
            var loadingSources = new List<Source>();
            foreach (var url in newUrls)
            {
                var isYouTube = url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                                url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

                var source = new Source
                {
                    Kind = isYouTube ? SourceKind.YouTube : SourceKind.Url,
                    Reference = url,
                    Title = "Fetching...",
                    Content = null
                };
                _db.Sources.Add(source);
                _db.DraftSources.Add(new DraftSource
                {
                    DraftId = draft.Id,
                    Source = source,
                    LinkedAt = DateTime.UtcNow
                });
                loadingSources.Add(source);
            }
            await _db.SaveChangesAsync();

            _queue.Enqueue(new BackgroundJobQueue.Job("url-scrape", async ct =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scopedScraper = scope.ServiceProvider.GetRequiredService<WebScraperService>();

                foreach (var ls in loadingSources)
                {
                    var scrape = await scopedScraper.ScrapeUrlAsync(ls.Reference);
                    var dbSource = await scopedDb.Sources.FindAsync(new object[] { ls.Id }, ct);
                    if (dbSource == null)
                    {
                        continue;
                    }

                    if (scrape.Success)
                    {
                        dbSource.Reference = scrape.FinalUrl;
                        dbSource.Title = scrape.Title;
                        dbSource.Content = scrape.Content;
                        dbSource.Kind = scrape.IsYouTube ? SourceKind.YouTube : SourceKind.Url;
                    }
                    else
                    {
                        dbSource.Title = $"Failed: {ls.Reference}";
                        dbSource.Content = $"Error fetching link: {scrape.Error}";
                    }
                }

                var d = await scopedDb.Drafts.FindAsync(new object[] { draft.Id }, ct);
                if (d != null)
                {
                    d.UpdatedAt = DateTime.UtcNow;
                }
                await scopedDb.SaveChangesAsync(ct);
            }));
        }
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
            QueueTranscriptExtraction(source.Id, draft.Id);
        }

        return new AddUrlSourceResult(source.Id, source.Reference, source.Title, source.Kind.ToString());
    }

    private void QueueTranscriptExtraction(Guid sourceId, Guid draftId)
    {
        _queue.Enqueue(new BackgroundJobQueue.Job("youtube-transcript", async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var transcriber = scope.ServiceProvider.GetRequiredService<ITranscriptExtractionService>();

            var source = await scopedDb.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
            if (source == null)
            {
                return;
            }

            source.TranscriptStatus = TranscriptStatus.Processing;
            await scopedDb.SaveChangesAsync(ct);

            try
            {
                var result = await transcriber.ExtractAsync(source.Reference, $"{source.Id}.json", ct);
                if (!result.Success || string.IsNullOrWhiteSpace(result.TranscriptPath))
                {
                    source.TranscriptStatus = TranscriptStatus.Failed;
                    source.Summary = result.Error;
                    await scopedDb.SaveChangesAsync(ct);
                    return;
                }

                source.TranscriptPath = result.TranscriptPath;

                var transcript = await transcriber.ReadTranscriptAsync(result.TranscriptPath, ct);
                if (transcript?.Transcript is { Length: > 0 } text)
                {
                    source.Content = text;
                }

                source.TranscriptStatus = TranscriptStatus.Complete;

                var draft = await scopedDb.Drafts.FindAsync(new object[] { draftId }, ct);
                if (draft != null)
                {
                    draft.UpdatedAt = DateTime.UtcNow;
                }

                await scopedDb.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                source.TranscriptStatus = TranscriptStatus.Failed;
                source.Summary = ex.Message;
                await scopedDb.SaveChangesAsync(ct);
            }
        }));
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
