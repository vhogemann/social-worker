using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.ReplyAgents;

public static class ReplyAgentsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflows/{workflowId:guid}/reply-agent")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid workflowId,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var workflow = await db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId);

            if (workflow == null) return Results.NotFound("Workflow not found.");

            if (workflow.ReplyAgent == null)
            {
                return Results.Ok<WorkflowReplyAgentDto?>(null);
            }

            return Results.Ok(new WorkflowReplyAgentDto(workflow.ReplyAgent));
        });

        group.MapPut("", async (
            Guid workflowId,
            UpdateWorkflowReplyAgentRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var workflow = await db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId);

            if (workflow == null) return Results.NotFound("Workflow not found.");

            var agent = workflow.ReplyAgent;
            if (agent == null)
            {
                agent = WorkflowReplyAgent.Create(
                    workflowId,
                    req.PersonaPrompt,
                    req.MaxCommentAgeHours,
                    req.MaxPostAgeDays,
                    req.MinRankingScore,
                    req.MaxRepliesPerCommentThread,
                    req.MaxRepliesPerUser,
                    req.MaxThreadDepth,
                    req.AutoPublishReplies,
                    req.CheckIntervalMinutes);
                agent.IsEnabled = req.IsEnabled;

                db.WorkflowReplyAgents.Add(agent);
            }
            else
            {
                agent.IsEnabled = req.IsEnabled;
                agent.PersonaPrompt = string.IsNullOrWhiteSpace(req.PersonaPrompt)
                    ? "You are a helpful and polite social media assistant."
                    : req.PersonaPrompt.Trim();
                agent.MaxCommentAgeHours = Math.Max(1, req.MaxCommentAgeHours);
                agent.MaxPostAgeDays = Math.Max(1, req.MaxPostAgeDays);
                agent.MinRankingScore = Math.Clamp(req.MinRankingScore, 1, 10);
                agent.MaxRepliesPerCommentThread = Math.Max(1, req.MaxRepliesPerCommentThread);
                agent.MaxRepliesPerUser = Math.Max(1, req.MaxRepliesPerUser);
                agent.MaxThreadDepth = Math.Max(1, req.MaxThreadDepth);
                agent.AutoPublishReplies = req.AutoPublishReplies;
                agent.CheckIntervalMinutes = Math.Max(1, req.CheckIntervalMinutes);
                agent.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new WorkflowReplyAgentDto(agent));
        });

        group.MapPost("/poll", async (
            Guid workflowId,
            ClaimsPrincipal user,
            AppDbContext db,
            IBackgroundTaskQueue taskQueue) =>
        {
            var userId = user.GetUserId();
            var workflow = await db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId);

            if (workflow == null) return Results.NotFound("Workflow not found.");
            if (workflow.ReplyAgent == null || !workflow.ReplyAgent.IsEnabled)
            {
                return Results.BadRequest("Reply Agent is not configured or disabled for this workflow.");
            }

            var agent = workflow.ReplyAgent;
            var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(agent.MaxPostAgeDays));
            var posts = await db.Posts
                .Include(p => p.Draft)
                .Where(p => p.Draft.AutomationWorkflowId == workflow.Id && p.RemoteId != null && p.PostedAt >= cutoff)
                .ToListAsync();

            var enqueuedCount = 0;
            foreach (var post in posts)
            {
                var payload = new WorkflowReplyCheckPostPayload(agent.Id, post.Id);
                var dedupKey = $"manual-reply-check-{agent.Id}-{post.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                await taskQueue.EnqueueAsync("workflow-reply-check-post", payload, EnqueueTaskOptions.WithDeduplication(dedupKey));
                enqueuedCount++;
            }

            return Results.Ok(new { success = true, postsChecked = enqueuedCount });
        });

        group.MapGet("/audits", async (
            Guid workflowId,
            int? limit,
            int? offset,
            string? decision,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var workflow = await db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId);

            if (workflow == null) return Results.NotFound("Workflow not found.");
            if (workflow.ReplyAgent == null) return Results.Ok(Array.Empty<WorkflowCommentAuditDto>());

            var query = db.WorkflowCommentAudits
                .Where(a => a.WorkflowReplyAgentId == workflow.ReplyAgent.Id);

            if (!string.IsNullOrWhiteSpace(decision))
            {
                query = query.Where(a => a.RankingDecision == decision);
            }

            var take = Math.Clamp(limit ?? 50, 1, 200);
            var skip = Math.Max(0, offset ?? 0);

            var audits = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return Results.Ok(audits.Select(a => new WorkflowCommentAuditDto(a)));
        });
    }
}
