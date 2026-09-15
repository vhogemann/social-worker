using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Abstractions;

namespace SocialWorker.Api.Features.Workflows.Services;

public sealed record WorkflowHighlightsConfig(int LookbackDays = 7, List<Guid>? SourceWorkflowIds = null);

public class DigestWorkflowSourceHandler : IWorkflowSourceHandler
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;
    private readonly FeedOrchestrationService _orchestrationService;
    private readonly ILogger<DigestWorkflowSourceHandler> _logger;

    public DigestWorkflowSourceHandler(
        AppDbContext db,
        DraftsService draftsService,
        FeedOrchestrationService orchestrationService,
        ILogger<DigestWorkflowSourceHandler> logger)
    {
        _db = db;
        _draftsService = draftsService;
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    public WorkflowSourceKind Kind => WorkflowSourceKind.WorkflowHighlights;

    public async Task<int> ProcessAsync(AutomationWorkflow workflow, AutomationWorkflowSource source, CancellationToken ct)
    {
        var targetWorkflowIds = new List<Guid>();
        var lookbackDays = 7;

        if (source.SourceWorkflowId.HasValue)
        {
            targetWorkflowIds.Add(source.SourceWorkflowId.Value);
        }

        if (!string.IsNullOrWhiteSpace(source.ConfigurationJson))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<WorkflowHighlightsConfig>(source.ConfigurationJson, options);
                if (config != null)
                {
                    if (config.LookbackDays > 0)
                    {
                        lookbackDays = config.LookbackDays;
                    }
                    if (config.SourceWorkflowIds != null && config.SourceWorkflowIds.Count > 0)
                    {
                        targetWorkflowIds.AddRange(config.SourceWorkflowIds);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ConfigurationJson for workflow highlights source {SourceId}", source.Id);
            }
        }

        targetWorkflowIds = targetWorkflowIds.Distinct().ToList();
        if (targetWorkflowIds.Count == 0)
        {
            _logger.LogInformation("Workflow highlights source {SourceId} has no target workflow IDs configured. Skipping.", source.Id);
            return 0;
        }

        var since = DateTime.UtcNow.AddDays(-lookbackDays);
        var filter = new DraftQueryFilter(
            WorkflowIds: targetWorkflowIds,
            IsPublished: true,
            Since: since
        );

        var publishedDrafts = await _draftsService.GetDraftsForUserAsync(workflow.UserId, filter, ct);
        if (publishedDrafts == null || publishedDrafts.Count == 0)
        {
            _logger.LogInformation("No published drafts found in the last {Days} days for workflow {WorkflowId}. Skipping digest draft generation.", lookbackDays, workflow.Id);
            return 0;
        }

        var digestBuilder = new StringBuilder();
        digestBuilder.AppendLine($"# Published Posts Digest (Last {lookbackDays} Days)");
        digestBuilder.AppendLine();

        foreach (var draft in publishedDrafts)
        {
            digestBuilder.AppendLine($"## {draft.Title}");
            if (!string.IsNullOrWhiteSpace(draft.AutomationWorkflowTitle))
            {
                digestBuilder.AppendLine($"*Workflow:* {draft.AutomationWorkflowTitle}");
            }

            var postUrls = new List<string>();
            if (draft.Threads != null)
            {
                foreach (var thread in draft.Threads)
                {
                    if (thread.Posts != null)
                    {
                        postUrls.AddRange(thread.Posts.Where(p => !string.IsNullOrWhiteSpace(p.Url)).Select(p => p.Url!));
                    }
                }
            }
            if (draft.Variants != null)
            {
                foreach (var variant in draft.Variants)
                {
                    if (variant.Threads != null)
                    {
                        foreach (var thread in variant.Threads)
                        {
                            if (thread.Posts != null)
                            {
                                postUrls.AddRange(thread.Posts.Where(p => !string.IsNullOrWhiteSpace(p.Url)).Select(p => p.Url!));
                            }
                        }
                    }
                }
            }

            var distinctUrls = postUrls.Distinct().ToList();
            if (distinctUrls.Count > 0)
            {
                digestBuilder.AppendLine($"*Published URLs:* {string.Join(", ", distinctUrls)}");
            }

            digestBuilder.AppendLine();
            digestBuilder.AppendLine("### Content");
            digestBuilder.AppendLine(draft.Content ?? "(No content recorded)");
            digestBuilder.AppendLine();
            digestBuilder.AppendLine("---");
            digestBuilder.AppendLine();
        }

        var digestContent = digestBuilder.ToString();
        var draftTitle = $"{workflow.Title} - {DateTime.UtcNow:yyyy-MM-dd}";

        var canonicalDraft = Draft.Create(
            workflow.UserId,
            draftTitle,
            null,
            SocialPlatform.Bluesky,
            automationWorkflowId: workflow.Id
        );
        _db.Drafts.Add(canonicalDraft);
        await _db.SaveChangesAsync(ct);

        var digestSource = Source.CreateFile(
            $"highlights_{workflow.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.md",
            title: $"Weekly Highlights Digest ({publishedDrafts.Count} posts)"
        );
        digestSource.Content = digestContent;
        digestSource.Summary = $"Summary digest containing {publishedDrafts.Count} published posts from the last {lookbackDays} days.";
        digestSource.ProcessingStatus = SourceProcessingStatus.Complete;
        _db.Sources.Add(digestSource);
        await _db.SaveChangesAsync(ct);

        _db.DraftSources.Add(new DraftSource
        {
            DraftId = canonicalDraft.Id,
            SourceId = digestSource.Id,
            LinkedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        // Ensure workflow destinations are loaded
        if (workflow.Destinations == null || workflow.Destinations.Count == 0)
        {
            await _db.Entry(workflow)
                .Collection(w => w.Destinations)
                .Query()
                .Include(d => d.Account)
                .Include(d => d.Schedule)
                .LoadAsync(ct);
        }

        await _orchestrationService.ProcessCanonicalDraftOrchestrationAsync(
            canonicalDraft,
            workflow,
            "internal://highlights",
            workflow.UserId,
            ct
        );

        return 1;
    }
}
