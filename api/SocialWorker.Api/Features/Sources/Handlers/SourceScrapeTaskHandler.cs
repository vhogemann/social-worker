using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.Sources.Handlers;

public sealed record SourceScrapePayload(Guid DraftId, List<Guid> SourceIds);

public sealed class SourceScrapeTaskHandler : BackgroundTaskHandlerBase<SourceScrapePayload>
{
    private readonly AppDbContext _db;
    private readonly WebScraperService _scraper;

    public override string TaskType => "source-scrape";
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(3);
    public override int DefaultMaxAttempts => 3;

    public SourceScrapeTaskHandler(AppDbContext db, WebScraperService scraper)
    {
        _db = db;
        _scraper = scraper;
    }

    public override async Task<TaskResult> ExecuteAsync(
        SourceScrapePayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        foreach (var sourceId in payload.SourceIds)
        {
            var dbSource = await _db.Sources.FindAsync(new object[] { sourceId }, ct);
            if (dbSource == null)
            {
                continue;
            }

            var scrape = await _scraper.ScrapeUrlAsync(dbSource.Reference);
            if (scrape.Success)
            {
                dbSource.Reference = scrape.FinalUrl;
                dbSource.Title = scrape.Title;
                dbSource.Content = scrape.Content;
                dbSource.Kind = scrape.IsYouTube ? SourceKind.YouTube : SourceKind.Url;
            }
            else
            {
                dbSource.Title = $"Failed: {dbSource.Reference}";
                dbSource.Content = $"Error fetching link: {scrape.Error}";
            }
        }

        var draft = await _db.Drafts.FindAsync(new object[] { payload.DraftId }, ct);
        if (draft != null)
        {
            draft.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return TaskResult.Success();
    }
}
