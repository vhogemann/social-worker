using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Workflows.Abstractions;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Validation;

namespace SocialWorker.Api.Features.Workflows;

public static class WorkflowsEndpoint
{
    public static void MapWorkflowsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workflows").RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflows = await db.AutomationWorkflows
                .Where(w => w.UserId == userId.Value)
                .Include(w => w.Schedule)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.SourceWorkflow)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Schedule)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync(ct);

            var dtos = workflows.Select(w => new AutomationWorkflowDto(w)).ToList();
            return Results.Ok(dtos);
        });

        group.MapPost("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CreateAutomationWorkflowRequest req,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflow = AutomationWorkflow.Create(userId.Value, req.Title, req.InstructionPrompt, req.ScheduleId);
            db.AutomationWorkflows.Add(workflow);

            if (req.Sources != null && req.Sources.Count > 0)
            {
                foreach (var src in req.Sources)
                {
                    if (src.Kind == WorkflowSourceKind.Feed && src.FeedSubscriptionId.HasValue)
                    {
                        var exists = await db.FeedSubscriptions.AnyAsync(s => s.Id == src.FeedSubscriptionId.Value && s.UserId == userId.Value, ct);
                        if (exists)
                        {
                            workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, src.FeedSubscriptionId.Value));
                        }
                    }
                    else if (src.Kind == WorkflowSourceKind.WorkflowHighlights)
                    {
                        Guid? sourceWfId = src.SourceWorkflowId;
                        if (sourceWfId.HasValue)
                        {
                            var exists = await db.AutomationWorkflows.AnyAsync(w => w.Id == sourceWfId.Value && w.UserId == userId.Value, ct);
                            if (!exists) sourceWfId = null;
                        }
                        workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateWorkflowHighlightsSource(workflow.Id, sourceWfId ?? Guid.Empty, src.ConfigurationJson));
                    }
                }
            }
            else if (req.FeedSubscriptionIds != null && req.FeedSubscriptionIds.Count > 0)
            {
                var validSubIds = await db.FeedSubscriptions
                    .Where(s => s.UserId == userId.Value && req.FeedSubscriptionIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var subId in validSubIds)
                {
                    workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, subId));
                }
            }

            if (req.Destinations != null)
            {
                foreach (var dest in req.Destinations)
                {
                    if (!string.IsNullOrWhiteSpace(dest.Platform))
                    {
                        var mode = dest.PublishingMode != WorkflowPublishingMode.Manual
                            ? dest.PublishingMode
                            : (dest.AutoPublish ? WorkflowPublishingMode.Automatic : WorkflowPublishingMode.Manual);
                        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, dest.Platform, mode, dest.ScheduleId, dest.AccountId));
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            var created = await db.AutomationWorkflows
                .Include(w => w.Schedule)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.SourceWorkflow)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Schedule)
                .FirstAsync(w => w.Id == workflow.Id, ct);

            return Results.Created($"/api/workflows/{workflow.Id}", new AutomationWorkflowDto(created));
        }).WithValidation<CreateAutomationWorkflowRequest>();

        group.MapGet("/{id:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflow = await db.AutomationWorkflows
                .Include(w => w.Schedule)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.SourceWorkflow)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Schedule)
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, ct);

            if (workflow == null) return Results.NotFound(new ApiErrorResponse("Workflow not found."));

            return Results.Ok(new AutomationWorkflowDto(workflow));
        });

        group.MapPut("/{id:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid id,
            CreateAutomationWorkflowRequest req,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflow = await db.AutomationWorkflows
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, ct);

            if (workflow == null) return Results.NotFound(new ApiErrorResponse("Workflow not found."));

            workflow.Title = string.IsNullOrWhiteSpace(req.Title) ? "Untitled Workflow" : req.Title.Trim();
            workflow.InstructionPrompt = req.InstructionPrompt ?? "";
            workflow.ScheduleId = req.ScheduleId;
            workflow.UpdatedAt = DateTime.UtcNow;

            var existingSources = await db.AutomationWorkflowSources
                .Where(s => s.AutomationWorkflowId == id)
                .ToListAsync(ct);
            db.AutomationWorkflowSources.RemoveRange(existingSources);

            if (req.Sources != null && req.Sources.Count > 0)
            {
                foreach (var src in req.Sources)
                {
                    if (src.Kind == WorkflowSourceKind.Feed && src.FeedSubscriptionId.HasValue)
                    {
                        var exists = await db.FeedSubscriptions.AnyAsync(s => s.Id == src.FeedSubscriptionId.Value && s.UserId == userId.Value, ct);
                        if (exists)
                        {
                            db.AutomationWorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, src.FeedSubscriptionId.Value));
                        }
                    }
                    else if (src.Kind == WorkflowSourceKind.WorkflowHighlights)
                    {
                        Guid? sourceWfId = src.SourceWorkflowId;
                        if (sourceWfId.HasValue)
                        {
                            var exists = await db.AutomationWorkflows.AnyAsync(w => w.Id == sourceWfId.Value && w.UserId == userId.Value, ct);
                            if (!exists) sourceWfId = null;
                        }
                        db.AutomationWorkflowSources.Add(AutomationWorkflowSource.CreateWorkflowHighlightsSource(workflow.Id, sourceWfId ?? Guid.Empty, src.ConfigurationJson));
                    }
                }
            }
            else if (req.FeedSubscriptionIds != null && req.FeedSubscriptionIds.Count > 0)
            {
                var validSubIds = await db.FeedSubscriptions
                    .Where(s => s.UserId == userId.Value && req.FeedSubscriptionIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var subId in validSubIds)
                {
                    db.AutomationWorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, subId));
                }
            }

            var existingDestinations = await db.AutomationWorkflowDestinations
                .Where(d => d.AutomationWorkflowId == id)
                .ToListAsync(ct);
            db.AutomationWorkflowDestinations.RemoveRange(existingDestinations);

            if (req.Destinations != null)
            {
                foreach (var dest in req.Destinations)
                {
                    if (!string.IsNullOrWhiteSpace(dest.Platform))
                    {
                        var mode = dest.PublishingMode != WorkflowPublishingMode.Manual
                            ? dest.PublishingMode
                            : (dest.AutoPublish ? WorkflowPublishingMode.Automatic : WorkflowPublishingMode.Manual);
                        db.AutomationWorkflowDestinations.Add(AutomationWorkflowDestination.Create(workflow.Id, dest.Platform, mode, dest.ScheduleId, dest.AccountId));
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            var updated = await db.AutomationWorkflows
                .Include(w => w.Schedule)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.SourceWorkflow)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Schedule)
                .FirstAsync(w => w.Id == workflow.Id, ct);

            return Results.Ok(new AutomationWorkflowDto(updated));
        }).WithValidation<CreateAutomationWorkflowRequest>();

        group.MapDelete("/{id:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflow = await db.AutomationWorkflows
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, ct);

            if (workflow == null) return Results.NotFound(new ApiErrorResponse("Workflow not found."));

            db.AutomationWorkflows.Remove(workflow);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/trigger", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IEnumerable<IWorkflowSourceHandler> sourceHandlers,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflow = await db.AutomationWorkflows
                .Include(w => w.WorkflowSources)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Schedule)
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, ct);

            if (workflow == null) return Results.NotFound(new ApiErrorResponse("Workflow not found."));

            var handlersList = sourceHandlers.ToList();
            foreach (var source in workflow.WorkflowSources)
            {
                var handler = handlersList.FirstOrDefault(h => h.Kind == source.Kind);
                if (handler != null)
                {
                    await handler.ProcessAsync(workflow, source, ct);
                }
            }

            return Results.Ok(new ApiSuccessResponse(true, "Workflow triggered successfully."));
        });

        group.MapGet("/{id:guid}/drafts", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflowExists = await db.AutomationWorkflows
                .AnyAsync(w => w.Id == id && w.UserId == userId.Value, ct);
            if (!workflowExists) return Results.NotFound(new ApiErrorResponse("Workflow not found."));

            var drafts = await db.Drafts
                .Include(d => d.Threads).ThenInclude(t => t.Account)
                .Include(d => d.Variants).ThenInclude(v => v.Threads).ThenInclude(t => t.Account)
                .Where(d => d.AutomationWorkflowId == id
                          && d.CanonicalDraftId == null
                          && d.Status != DraftStatus.Deleted)
                .OrderByDescending(d => d.CreatedAt)
                .Take(100)
                .ToListAsync(ct);

            var dtos = drafts.Select(d => new WorkflowDraftSummaryDto(d)).ToList();
            return Results.Ok(dtos);
        });
    }
}

public sealed record WorkflowSourceDto(
    Guid Id,
    WorkflowSourceKind Kind,
    Guid? FeedSubscriptionId,
    string? Title,
    string? FeedUrl,
    Guid? SourceWorkflowId = null,
    string? SourceWorkflowTitle = null,
    string? ConfigurationJson = null)
{
    public WorkflowSourceDto(AutomationWorkflowSource s)
        : this(
            s.Id,
            s.Kind,
            s.FeedSubscriptionId,
            s.Kind == WorkflowSourceKind.Feed ? (s.FeedSubscription?.Title ?? "") : (s.Kind == WorkflowSourceKind.WorkflowHighlights ? (s.SourceWorkflow?.Title ?? "") : ""),
            s.FeedSubscription?.FeedUrl,
            s.SourceWorkflowId,
            s.SourceWorkflow?.Title,
            s.ConfigurationJson)
    {
    }
}

public sealed record WorkflowDestinationDto(
    Guid Id,
    string Platform,
    bool AutoPublish,
    WorkflowPublishingMode PublishingMode,
    Guid? ScheduleId = null,
    string? ScheduleName = null,
    Guid? AccountId = null,
    string? AccountHandle = null)
{
    public WorkflowDestinationDto(AutomationWorkflowDestination d)
        : this(d.Id, d.Platform, d.AutoPublish, d.PublishingMode, d.ScheduleId, d.Schedule?.Name, d.AccountId, d.Account?.Handle)
    {
    }
}

public sealed record AutomationWorkflowDto(
    Guid Id,
    string Title,
    string InstructionPrompt,
    List<WorkflowSourceDto> Sources,
    List<WorkflowDestinationDto> Destinations,
    Guid? ScheduleId,
    string? ScheduleName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public AutomationWorkflowDto(AutomationWorkflow w)
        : this(
            w.Id,
            w.Title,
            w.InstructionPrompt,
            w.WorkflowSources.Select(s => new WorkflowSourceDto(s)).ToList(),
            w.Destinations.Select(d => new WorkflowDestinationDto(d)).ToList(),
            w.ScheduleId,
            w.Schedule?.Name,
            w.CreatedAt,
            w.UpdatedAt)
    {
    }
}

public sealed record CreateWorkflowSourceRequest(
    WorkflowSourceKind Kind = WorkflowSourceKind.Feed,
    Guid? FeedSubscriptionId = null,
    Guid? SourceWorkflowId = null,
    string? ConfigurationJson = null);

public sealed record CreateWorkflowDestinationRequest(
    string Platform,
    bool AutoPublish = false,
    WorkflowPublishingMode PublishingMode = WorkflowPublishingMode.Manual,
    Guid? ScheduleId = null,
    Guid? AccountId = null);

public sealed record CreateAutomationWorkflowRequest(
    string Title,
    string? InstructionPrompt,
    List<Guid>? FeedSubscriptionIds = null,
    List<CreateWorkflowSourceRequest>? Sources = null,
    List<CreateWorkflowDestinationRequest>? Destinations = null,
    Guid? ScheduleId = null);

public sealed record WorkflowDraftThreadDto(
    string Platform,
    string Stage,
    string? AccountHandle,
    Guid? VariantId = null)
{
    public WorkflowDraftThreadDto(PlatformThread t, Guid? variantId = null)
        : this(t.Platform, t.Stage.ToString(), t.Account?.Handle, variantId)
    {
    }
}

public sealed record WorkflowDraftSummaryDto(
    Guid Id,
    string Title,
    string Status,
    string? TargetPlatform,
    List<WorkflowDraftThreadDto> Threads,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public WorkflowDraftSummaryDto(Draft d)
        : this(
            d.Id,
            d.Title,
            d.Status.ToString(),
            d.TargetPlatform?.ToString(),
            BuildThreads(d),
            d.CreatedAt,
            d.UpdatedAt)
    {
    }

    private static List<WorkflowDraftThreadDto> BuildThreads(Draft d)
    {
        var result = new List<WorkflowDraftThreadDto>();

        if (d.Threads != null)
        {
            foreach (var t in d.Threads)
            {
                result.Add(new WorkflowDraftThreadDto(t, null));
            }
        }

        if (d.Variants != null)
        {
            foreach (var v in d.Variants.Where(x => x.Status != DraftStatus.Deleted))
            {
                if (v.Threads != null && v.Threads.Count > 0)
                {
                    foreach (var vt in v.Threads)
                    {
                        var existingIndex = result.FindIndex(r => r.Platform == vt.Platform);
                        if (existingIndex >= 0)
                        {
                            result[existingIndex] = new WorkflowDraftThreadDto(vt, v.Id);
                        }
                        else
                        {
                            result.Add(new WorkflowDraftThreadDto(vt, v.Id));
                        }
                    }
                }
                else if (v.TargetPlatform.HasValue)
                {
                    var pName = v.TargetPlatform.Value.ToString();
                    var existingIndex = result.FindIndex(r => r.Platform == pName);
                    if (existingIndex >= 0)
                    {
                        result[existingIndex] = result[existingIndex] with { VariantId = v.Id };
                    }
                    else
                    {
                        result.Add(new WorkflowDraftThreadDto(pName, v.Status.ToString(), null, v.Id));
                    }
                }
            }
        }

        return result;
    }
}
