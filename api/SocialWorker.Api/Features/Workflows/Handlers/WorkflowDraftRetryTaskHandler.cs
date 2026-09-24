using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Feeds;

namespace SocialWorker.Api.Features.Workflows.Handlers;

public sealed record WorkflowDraftRetryPayload(Guid DraftId);

public sealed class WorkflowDraftRetryTaskHandler : BackgroundTaskHandlerBase<WorkflowDraftRetryPayload>
{
    private readonly AppDbContext _db;
    private readonly FeedOrchestrationService _orchestrator;
    private readonly ILogger<WorkflowDraftRetryTaskHandler> _logger;

    public const string TaskTypeName = "workflow-draft-retry";
    public override string TaskType => TaskTypeName;
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    public override int DefaultMaxAttempts => 3;

    public WorkflowDraftRetryTaskHandler(
        AppDbContext db,
        FeedOrchestrationService orchestrator,
        ILogger<WorkflowDraftRetryTaskHandler> logger)
    {
        _db = db;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        WorkflowDraftRetryPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var draft = await _db.Drafts
            .Include(d => d.AutomationWorkflow!)
                .ThenInclude(w => w.Destinations)
            .Include(d => d.DraftSources)
                .ThenInclude(ds => ds.Source)
            .FirstOrDefaultAsync(d => d.Id == payload.DraftId && d.Status != DraftStatus.Deleted, ct);

        if (draft == null || draft.AutomationWorkflow == null)
        {
            return TaskResult.Fatal($"Draft {payload.DraftId} not found or has no automation workflow.");
        }

        var oldVariants = await _db.Drafts
            .Where(v => v.CanonicalDraftId == draft.Id && v.Status != DraftStatus.Deleted)
            .ToListAsync(ct);
        if (oldVariants.Count > 0)
        {
            _db.Drafts.RemoveRange(oldVariants);
            await _db.SaveChangesAsync(ct);
        }

        var primarySource = draft.DraftSources.Select(ds => ds.Source).FirstOrDefault();
        var itemLink = primarySource?.Reference ?? string.Empty;

        draft.Status = DraftStatus.Formatting;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _orchestrator.ProcessCanonicalDraftOrchestrationAsync(
                draft,
                draft.AutomationWorkflow,
                itemLink,
                draft.UserId,
                ct);

            return TaskResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry workflow draft {DraftId}", payload.DraftId);
            return TaskResult.Retry(TimeSpan.FromSeconds(30), ex.Message);
        }
    }
}
