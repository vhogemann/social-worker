using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Workflows;

public interface IWorkflowPortabilityService
{
    Task<WorkflowExportPackage> ExportAsync(Guid userId, Guid? workflowId = null, CancellationToken ct = default);
    Task<WorkflowImportResult> ImportAsync(Guid userId, WorkflowExportPackage package, CancellationToken ct = default);
}

public sealed class WorkflowPortabilityService : IWorkflowPortabilityService
{
    private readonly AppDbContext _db;

    public WorkflowPortabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowExportPackage> ExportAsync(Guid userId, Guid? workflowId = null, CancellationToken ct = default)
    {
        var query = _db.AutomationWorkflows
            .Where(w => w.UserId == userId);

        if (workflowId.HasValue)
        {
            query = query.Where(w => w.Id == workflowId.Value);
        }

        var workflows = await query
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        var exportedWorkflows = workflows.Select(w => new ExportedWorkflow(
            w.Title,
            w.InstructionPrompt,
            w.MinRelevanceScore,
            w.WorkflowSources.Select(s => new ExportedWorkflowSource(
                s.Kind,
                s.Kind == WorkflowSourceKind.Feed && s.FeedSubscription != null
                    ? new ExportedFeedDefinition(
                        s.FeedSubscription.Title,
                        s.FeedSubscription.FeedUrl,
                        s.FeedSubscription.WebsiteUrl,
                        s.FeedSubscription.IncludeFilters,
                        s.FeedSubscription.ExcludeFilters)
                    : null,
                s.ConfigurationJson
            )).ToList(),
            w.Destinations.Select(d => new ExportedWorkflowDestination(
                d.Platform,
                d.AutoPublish,
                d.PublishingMode
            )).ToList()
        )).ToList();

        return new WorkflowExportPackage(1, DateTimeOffset.UtcNow, exportedWorkflows);
    }

    public async Task<WorkflowImportResult> ImportAsync(Guid userId, WorkflowExportPackage package, CancellationToken ct = default)
    {
        if (package == null || package.Workflows == null || package.Workflows.Count == 0)
        {
            return new WorkflowImportResult(0, 0, 0, new List<Guid>());
        }

        var importedIds = new List<Guid>();
        int createdFeedsCount = 0;
        int reusedFeedsCount = 0;

        var existingFeeds = await _db.FeedSubscriptions
            .Where(f => f.UserId == userId)
            .ToListAsync(ct);
        var feedByUrl = existingFeeds.ToDictionary(f => f.FeedUrl.Trim().ToLowerInvariant(), f => f);

        foreach (var exportedWf in package.Workflows)
        {
            if (string.IsNullOrWhiteSpace(exportedWf.Title))
                continue;

            var workflow = AutomationWorkflow.Create(
                userId,
                exportedWf.Title,
                exportedWf.InstructionPrompt,
                null,
                exportedWf.MinRelevanceScore);

            _db.AutomationWorkflows.Add(workflow);

            if (exportedWf.Sources != null)
            {
                foreach (var src in exportedWf.Sources)
                {
                    if (src.Kind == WorkflowSourceKind.Feed && src.Feed != null && !string.IsNullOrWhiteSpace(src.Feed.FeedUrl))
                    {
                        var normalizedUrl = src.Feed.FeedUrl.Trim().ToLowerInvariant();
                        if (feedByUrl.TryGetValue(normalizedUrl, out var existingFeed))
                        {
                            workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, existingFeed.Id));
                            reusedFeedsCount++;
                        }
                        else
                        {
                            var newFeed = FeedSubscription.Create(
                                userId,
                                src.Feed.Title,
                                src.Feed.FeedUrl,
                                src.Feed.WebsiteUrl,
                                src.Feed.IncludeFilters,
                                src.Feed.ExcludeFilters);

                            _db.FeedSubscriptions.Add(newFeed);
                            feedByUrl[normalizedUrl] = newFeed;
                            workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, newFeed.Id));
                            createdFeedsCount++;
                        }
                    }
                    else if (src.Kind == WorkflowSourceKind.WorkflowHighlights)
                    {
                        workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateWorkflowHighlightsSource(workflow.Id, Guid.Empty, src.ConfigurationJson));
                    }
                }
            }

            if (exportedWf.Destinations != null)
            {
                foreach (var dest in exportedWf.Destinations)
                {
                    if (!string.IsNullOrWhiteSpace(dest.Platform))
                    {
                        var mode = dest.PublishingMode != WorkflowPublishingMode.Manual
                            ? dest.PublishingMode
                            : (dest.AutoPublish ? WorkflowPublishingMode.Automatic : WorkflowPublishingMode.Manual);

                        workflow.Destinations.Add(AutomationWorkflowDestination.Create(
                            workflow.Id,
                            dest.Platform,
                            mode,
                            null,
                            null));
                    }
                }
            }

            importedIds.Add(workflow.Id);
        }

        await _db.SaveChangesAsync(ct);

        return new WorkflowImportResult(importedIds.Count, createdFeedsCount, reusedFeedsCount, importedIds);
    }
}
