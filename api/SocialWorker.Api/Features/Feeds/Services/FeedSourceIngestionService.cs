using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Feeds;

public enum IngestionStatus
{
    Success,
    Duplicate,
    FilteredOut,
    Failed,
    PendingTranscription
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
    private readonly SourceTranscriptionService? _transcriptionService;

    public FeedSourceIngestionService(
        AppDbContext db,
        SourcesService sourcesService,
        ILogger<FeedSourceIngestionService> logger,
        SourceTranscriptionService? transcriptionService = null)
    {
        _db = db;
        _sourcesService = sourcesService;
        _logger = logger;
        _transcriptionService = transcriptionService;
    }

    public virtual Task<FeedSourceIngestionResult> IngestSourceAsync(
        FeedSubscription subscription,
        AutomationWorkflow workflow,
        string itemTitle,
        string itemLink,
        string itemDescription,
        CancellationToken ct)
    {
        return IngestSourceInternalAsync(subscription, workflow, itemTitle, itemLink, itemDescription, ct, null, null);
    }

    public virtual Task<FeedSourceIngestionResult> IngestSourceAsync(
        FeedSubscription subscription,
        AutomationWorkflow workflow,
        string itemTitle,
        string itemLink,
        string itemDescription,
        CancellationToken ct,
        string? mediaUrl,
        string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl) && string.IsNullOrWhiteSpace(mediaType))
        {
            return IngestSourceAsync(subscription, workflow, itemTitle, itemLink, itemDescription, ct);
        }

        return IngestSourceInternalAsync(subscription, workflow, itemTitle, itemLink, itemDescription, ct, mediaUrl, mediaType);
    }

    private async Task<FeedSourceIngestionResult> IngestSourceInternalAsync(
        FeedSubscription subscription,
        AutomationWorkflow workflow,
        string itemTitle,
        string itemLink,
        string itemDescription,
        CancellationToken ct,
        string? mediaUrl,
        string? mediaType)
    {
        var userId = subscription.UserId;

        var isDuplicate = await _db.Sources.AnyAsync(s =>
            s.Reference == itemLink &&
            s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.AutomationWorkflowId == workflow.Id && ds.Draft.Status != DraftStatus.Deleted),
            ct);

        if (isDuplicate)
        {
            _logger.LogInformation("Skipping duplicate feed item link {Link} for workflow {WorkflowId}", itemLink, workflow.Id);
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
            Title = string.IsNullOrWhiteSpace(itemTitle) ? "Untitled Feed Item" : itemTitle.Trim(),
            Status = DraftStatus.Editing,
            UserId = userId,
            TargetPlatform = null,
            AutomationWorkflowId = workflow.Id
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        Guid sourceId;
        var isInstagram = itemLink.Contains("instagram.com", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(subscription.FeedUrl) && subscription.FeedUrl.StartsWith("instagram://", StringComparison.OrdinalIgnoreCase));

        try
        {
            var initialContent = isInstagram
                ? (!string.IsNullOrWhiteSpace(itemDescription) ? itemDescription : $"[Instagram post by {itemTitle}]\nLink: {itemLink}")
                : null;

            var addResult = await _sourcesService.AddUrlSourceAsync(userId, draft.Id, itemLink, itemTitle, initialContent, ct);
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
            return new FeedSourceIngestionResult(IngestionStatus.PendingTranscription, draft, sourceId, null);
        }

        var isInstagramVideo = isInstagram && (
            string.Equals(mediaType, "VIDEO", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(mediaUrl) && (mediaUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase)))
        );

        if (source != null && isInstagramVideo && !string.IsNullOrWhiteSpace(mediaUrl) && _transcriptionService != null)
        {
            _transcriptionService.QueueTranscriptExtraction(sourceId, draft.Id, mediaUrl);
            return new FeedSourceIngestionResult(IngestionStatus.PendingTranscription, draft, sourceId, null);
        }

        return new FeedSourceIngestionResult(IngestionStatus.Success, draft, sourceId, null);
    }


    public static bool PassesFilters(string title, string description, string? includeFilters, string? excludeFilters)
    {
        var text = $"{title} {description}".ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(includeFilters))
        {
            var includes = includeFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                         .Select(s => s.ToLowerInvariant())
                                         .ToList();
            if (includes.Count > 0 && !includes.Any(inc => text.Contains(inc)))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(excludeFilters))
        {
            var excludes = excludeFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                         .Select(s => s.ToLowerInvariant())
                                         .ToList();
            if (excludes.Any(exc => text.Contains(exc)))
            {
                return false;
            }
        }

        return true;
    }
}
