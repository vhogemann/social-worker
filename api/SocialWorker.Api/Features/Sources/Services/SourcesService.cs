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

namespace SocialWorker.Api.Features.Sources;

public sealed record SourceDto(Guid Id, Guid DraftId, string Kind, string Reference, string? Title, DateTime AddedAt);

public sealed record AddFileSourceResult(Guid SourceId, string Reference);

public sealed class SourcesService
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s\)\""'<>]+", RegexOptions.Compiled);
    private static readonly Regex FileRegex = new(@"file://([0-9a-fA-F\-]{36})", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly WebScraperService _scraper;
    private readonly IServiceScopeFactory _scopeFactory;

    public SourcesService(AppDbContext db, WebScraperService scraper, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _scraper = scraper;
        _scopeFactory = scopeFactory;
    }

    public async Task<List<SourceDto>> GetSourcesForDraftAsync(Guid userId, Guid draftId, CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        return await _db.Sources
            .Where(s => s.DraftId == draftId)
            .Select(s => new SourceDto(s.Id, s.DraftId, s.Kind.ToString(), s.Reference, s.Title, s.AddedAt))
            .ToListAsync(ct);
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
            var source = new Source
            {
                DraftId = draftId,
                Kind = SourceKind.File,
                Reference = fileName,
                Title = existing.Title ?? fileName,
                Content = existing.Content,
                Sha256 = shaHashStr,
                AddedAt = DateTime.UtcNow
            };

            _db.Sources.Add(source);
            draft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);

            return new AddFileSourceResult(source.Id, source.Reference);
        }

        draft.Status = DraftStatus.Sourcing;
        await _db.SaveChangesAsync(ct);

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
            draft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        var newSource = new Source
        {
            DraftId = draftId,
            Kind = SourceKind.File,
            Reference = fileName,
            Title = fileName,
            Content = extractedText,
            Sha256 = shaHashStr,
            AddedAt = DateTime.UtcNow
        };

        _db.Sources.Add(newSource);
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
            .Where(s => s.DraftId == draft.Id)
            .ToListAsync();

        bool changed = false;
        foreach (var src in existing)
        {
            if ((src.Kind == SourceKind.Url || src.Kind == SourceKind.YouTube) && !urls.Contains(src.Reference))
            {
                _db.Sources.Remove(src);
                changed = true;
            }
            else if (src.Kind == SourceKind.File && !fileIds.Contains(src.Id))
            {
                _db.Sources.Remove(src);
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
            draft.Status = DraftStatus.Sourcing;
            await _db.SaveChangesAsync();

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scopedScraper = scope.ServiceProvider.GetRequiredService<WebScraperService>();

                foreach (var url in newUrls)
                {
                    try
                    {
                        var (title, contentText, isYouTube) = await scopedScraper.ScrapeUrlAsync(url);
                        var source = new Source
                        {
                            DraftId = draft.Id,
                            Kind = isYouTube ? SourceKind.YouTube : SourceKind.Url,
                            Reference = url,
                            Title = title,
                            Content = contentText
                        };
                        scopedDb.Sources.Add(source);
                    }
                    catch (Exception ex)
                    {
                        var isYouTube = url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                                        url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

                        var source = new Source
                        {
                            DraftId = draft.Id,
                            Kind = isYouTube ? SourceKind.YouTube : SourceKind.Url,
                            Reference = url,
                            Title = url,
                            Content = $"Error fetching link: {ex.Message}"
                        };
                        scopedDb.Sources.Add(source);
                    }
                }

                var d = await scopedDb.Drafts.FindAsync(draft.Id);
                if (d != null)
                {
                    d.Status = DraftStatus.Editing;
                    d.UpdatedAt = DateTime.UtcNow;
                }
                await scopedDb.SaveChangesAsync();
            });
        }
    }
}
