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
using SocialWorker.Api.Features.Feeds;
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
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
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

            var workflow = AutomationWorkflow.Create(userId.Value, req.Title, req.InstructionPrompt);
            db.AutomationWorkflows.Add(workflow);

            if (req.FeedSubscriptionIds != null && req.FeedSubscriptionIds.Count > 0)
            {
                var validSubIds = await db.FeedSubscriptions
                    .Where(s => s.UserId == userId.Value && req.FeedSubscriptionIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var subId in validSubIds)
                {
                    workflow.WorkflowSources.Add(AutomationWorkflowSource.Create(workflow.Id, subId));
                }
            }

            if (req.Destinations != null)
            {
                foreach (var dest in req.Destinations)
                {
                    if (!string.IsNullOrWhiteSpace(dest.Platform))
                    {
                        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, dest.Platform, dest.AutoPublish, dest.AccountId));
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            // Re-query with includes to return full DTO
            var created = await db.AutomationWorkflows
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
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
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, ct);

            if (workflow == null) return Results.NotFound();

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

            if (workflow == null) return Results.NotFound();

            workflow.Title = string.IsNullOrWhiteSpace(req.Title) ? "Untitled Workflow" : req.Title.Trim();
            workflow.InstructionPrompt = req.InstructionPrompt ?? "";
            workflow.UpdatedAt = DateTime.UtcNow;

            // Sync feed subscriptions
            var existingSources = await db.AutomationWorkflowSources
                .Where(s => s.AutomationWorkflowId == id)
                .ToListAsync(ct);
            db.AutomationWorkflowSources.RemoveRange(existingSources);

            if (req.FeedSubscriptionIds != null && req.FeedSubscriptionIds.Count > 0)
            {
                var validSubIds = await db.FeedSubscriptions
                    .Where(s => s.UserId == userId.Value && req.FeedSubscriptionIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var subId in validSubIds)
                {
                    db.AutomationWorkflowSources.Add(AutomationWorkflowSource.Create(workflow.Id, subId));
                }
            }

            // Sync destinations
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
                        db.AutomationWorkflowDestinations.Add(AutomationWorkflowDestination.Create(workflow.Id, dest.Platform, dest.AutoPublish, dest.AccountId));
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            var updated = await db.AutomationWorkflows
                .Include(w => w.WorkflowSources)
                    .ThenInclude(ws => ws.FeedSubscription)
                .Include(w => w.Destinations)
                    .ThenInclude(d => d.Account)
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

            if (workflow == null) return Results.NotFound();

            db.AutomationWorkflows.Remove(workflow);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/trigger", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            FeedPollingService pollingService,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var workflow = await db.AutomationWorkflows
                .Include(w => w.WorkflowSources)
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, ct);

            if (workflow == null) return Results.NotFound();

            foreach (var source in workflow.WorkflowSources)
            {
                await pollingService.PollSubscriptionAsync(source.FeedSubscriptionId, ct);
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
            if (!workflowExists) return Results.NotFound();

            var drafts = await db.Drafts
                .Include(d => d.Threads).ThenInclude(t => t.Account)
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

public sealed record WorkflowSourceDto(Guid FeedSubscriptionId, string Title, string FeedUrl);

public sealed record WorkflowDestinationDto(Guid Id, string Platform, bool AutoPublish, Guid? AccountId = null, string? AccountHandle = null)
{
    public WorkflowDestinationDto(AutomationWorkflowDestination d)
        : this(d.Id, d.Platform, d.AutoPublish, d.AccountId, d.Account?.Handle)
    {
    }
}

public sealed record AutomationWorkflowDto(
    Guid Id,
    string Title,
    string InstructionPrompt,
    List<WorkflowSourceDto> Sources,
    List<WorkflowDestinationDto> Destinations,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public AutomationWorkflowDto(AutomationWorkflow w)
        : this(
            w.Id,
            w.Title,
            w.InstructionPrompt,
            w.WorkflowSources.Select(s => new WorkflowSourceDto(
                s.FeedSubscriptionId,
                s.FeedSubscription?.Title ?? "",
                s.FeedSubscription?.FeedUrl ?? "")).ToList(),
            w.Destinations.Select(d => new WorkflowDestinationDto(d)).ToList(),
            w.CreatedAt,
            w.UpdatedAt)
    {
    }
}

public sealed record CreateWorkflowDestinationRequest(string Platform, bool AutoPublish, Guid? AccountId = null);

public sealed record CreateAutomationWorkflowRequest(
    string Title,
    string? InstructionPrompt,
    List<Guid>? FeedSubscriptionIds,
    List<CreateWorkflowDestinationRequest>? Destinations);

public sealed record WorkflowDraftThreadDto(
    string Platform,
    string Stage,
    string? AccountHandle)
{
    public WorkflowDraftThreadDto(PlatformThread t)
        : this(t.Platform, t.Stage.ToString(), t.Account?.Handle)
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
            d.Threads?.Select(t => new WorkflowDraftThreadDto(t)).ToList() ?? new List<WorkflowDraftThreadDto>(),
            d.CreatedAt,
            d.UpdatedAt)
    {
    }
}
