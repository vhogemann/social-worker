using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Feeds;

public enum IngestionStatus
{
    Success,
    Duplicate,
    FilteredOut,
    Failed
}

public sealed record FeedSourceIngestionResult(
    IngestionStatus Status,
    Draft? Draft,
    Guid? SourceId,
    string? ErrorMessage);

public class FeedSourceIngestionService
{
    private readonly AppDbContext _db;
    private readonly SourcesService _sourcesService;
    private readonly ILogger<FeedSourceIngestionService> _logger;

    public FeedSourceIngestionService(
        AppDbContext db,
        SourcesService sourcesService,
        ILogger<FeedSourceIngestionService> logger)
    {
        _db = db;
        _sourcesService = sourcesService;
        _logger = logger;
    }

    public async Task<FeedSourceIngestionResult> IngestSourceAsync(
        FeedSubscription subscription,
        string itemTitle,
        string itemLink,
        string itemDescription,
        CancellationToken ct)
    {
        var userId = subscription.UserId;

        var isDuplicate = await _db.Sources.AnyAsync(s =>
            s.Reference == itemLink &&
            s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted),
            ct);

        if (isDuplicate)
        {
            _logger.LogInformation("Skipping duplicate feed item link: {Link}", itemLink);
            return new FeedSourceIngestionResult(IngestionStatus.Duplicate, null, null, "Duplicate feed item.");
        }

        if (!PassesFilters(itemTitle, itemDescription, subscription.IncludeFilters, subscription.ExcludeFilters))
        {
            _logger.LogInformation("Skipping feed item {Link} because it does not match filters.", itemLink);
            return new FeedSourceIngestionResult(IngestionStatus.FilteredOut, null, null, "Item did not match subscription filters.");
        }

        _logger.LogInformation("Processing new feed item: {Link}", itemLink);

        var draft = new Draft
        {
            Title = string.IsNullOrWhiteSpace(itemTitle) ? "Untitled Feed Item" : itemTitle,
            Status = DraftStatus.Sourcing,
            UserId = userId,
            TargetPlatform = SocialPlatform.Bluesky
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        var thread = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = string.Empty
        };
        _db.PlatformThreads.Add(thread);
        await _db.SaveChangesAsync(ct);

        Guid sourceId;
        try
        {
            var addResult = await _sourcesService.AddUrlSourceAsync(userId, draft.Id, itemLink, itemTitle, null, ct);
            sourceId = addResult.SourceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest source URL {Link} for draft {DraftId}. Aborting orchestration.", itemLink, draft.Id);
            draft.Status = DraftStatus.Failed;
            draft.Title = $"[Failed Ingestion] {draft.Title}";
            await _db.SaveChangesAsync(ct);
            return new FeedSourceIngestionResult(IngestionStatus.Failed, draft, null, ex.Message);
        }

        var source = await _db.Sources.FindAsync(new object[] { sourceId }, ct);
        if (source != null && source.Kind == SourceKind.YouTube)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMinutes(10);
            while (true)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    _logger.LogError("Ingestion gate timeout waiting for YouTube transcription of source {SourceId}", sourceId);
                    draft.Status = DraftStatus.Failed;
                    draft.Title = $"[Timeout Ingestion] {draft.Title}";
                    await _db.SaveChangesAsync(ct);
                    return new FeedSourceIngestionResult(IngestionStatus.Failed, draft, sourceId, "YouTube transcription timed out.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                var currentSource = await _db.Sources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sourceId, ct);
                if (currentSource == null)
                {
                    _logger.LogError("Source {SourceId} was deleted during ingestion waiting.", sourceId);
                    draft.Status = DraftStatus.Failed;
                    await _db.SaveChangesAsync(ct);
                    return new FeedSourceIngestionResult(IngestionStatus.Failed, draft, sourceId, "Source deleted during ingestion.");
                }

                if (currentSource.ProcessingStatus == SourceProcessingStatus.Complete)
                {
                    break;
                }

                if (currentSource.ProcessingStatus == SourceProcessingStatus.Failed)
                {
                    _logger.LogError("YouTube transcription failed for source {SourceId}.", sourceId);
                    draft.Status = DraftStatus.Failed;
                    draft.Title = $"[Failed Ingestion] {draft.Title}";
                    await _db.SaveChangesAsync(ct);
                    return new FeedSourceIngestionResult(IngestionStatus.Failed, draft, sourceId, "YouTube transcription failed.");
                }
            }
        }

        return new FeedSourceIngestionResult(IngestionStatus.Success, draft, sourceId, null);
    }

    public static bool PassesFilters(string title, string description, string? includeFilters, string? excludeFilters)
    {
        var textToMatch = $"{(title ?? "")} {(description ?? "")}";

        if (!string.IsNullOrWhiteSpace(excludeFilters))
        {
            var excludes = excludeFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var exc in excludes)
            {
                if (textToMatch.Contains(exc, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(includeFilters))
        {
            var includes = includeFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var inc in includes)
            {
                if (textToMatch.Contains(inc, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        return true;
    }
}
