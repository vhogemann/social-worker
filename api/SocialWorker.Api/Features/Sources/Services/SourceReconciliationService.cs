using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceReconciliationService
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s\)\""'<>]+", RegexOptions.Compiled);
    private static readonly Regex FileRegex = new(@"file://([0-9a-fA-F\-]{36})", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundJobQueue _queue;

    public SourceReconciliationService(AppDbContext db, IServiceScopeFactory scopeFactory, BackgroundJobQueue queue)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _queue = queue;
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
                                url.Contains("youtube.com/shorts/", StringComparison.OrdinalIgnoreCase) ||
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
}