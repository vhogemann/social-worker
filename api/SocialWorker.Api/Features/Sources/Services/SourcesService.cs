using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Sources;

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
