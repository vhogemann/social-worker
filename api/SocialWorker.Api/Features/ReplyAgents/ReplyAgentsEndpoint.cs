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
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
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
                    .ThenInclude(a => a!.Workflows)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId);

            if (workflow == null) return Results.NotFound("Workflow not found.");

            if (workflow.ReplyAgent == null)
            {
                return Results.Ok<ReplyAgentDto?>(null);
            }

            return Results.Ok(new ReplyAgentDto(workflow.ReplyAgent));
        });

        group.MapPut("", async (
            Guid workflowId,
            AssignWorkflowReplyAgentRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var workflow = await db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId);

            if (workflow == null) return Results.NotFound("Workflow not found.");

            if (req.ReplyAgentId.HasValue)
            {
                var agent = await db.ReplyAgents
                    .Include(a => a.Workflows)
                    .FirstOrDefaultAsync(a => a.Id == req.ReplyAgentId.Value && a.UserId == userId);

                if (agent == null) return Results.NotFound("Reply Agent not found.");

                workflow.ReplyAgentId = agent.Id;
                await db.SaveChangesAsync();

                return Results.Ok(new ReplyAgentDto(agent));
            }
            else
            {
                workflow.ReplyAgentId = null;
                await db.SaveChangesAsync();

                return Results.Ok<ReplyAgentDto?>(null);
            }
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
                .Include(a => a.Post)
                    .ThenInclude(p => p.Draft)
                .Where(a => a.ReplyAgentId == workflow.ReplyAgent.Id
                    && a.Post.Draft.AutomationWorkflowId == workflow.Id);

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

        group.MapPost("/audits/{auditId:guid}/reply", async (
            Guid workflowId,
            Guid auditId,
            ApproveCommentReplyRequest? req,
            ClaimsPrincipal user,
            AppDbContext db,
            IReplyPlatformAdapterResolver adapterResolver,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var workflow = await db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

            if (workflow == null) return Results.NotFound("Workflow not found.");
            if (workflow.ReplyAgent == null) return Results.NotFound("Reply Agent not found.");

            var audit = await db.WorkflowCommentAudits
                .Include(a => a.Post)
                    .ThenInclude(p => p.PlatformThread)
                        .ThenInclude(pt => pt.Account)
                .Include(a => a.Post)
                    .ThenInclude(p => p.Draft)
                .FirstOrDefaultAsync(a => a.Id == auditId
                    && a.ReplyAgentId == workflow.ReplyAgent.Id
                    && a.Post.Draft.AutomationWorkflowId == workflow.Id, ct);

            if (audit == null) return Results.NotFound("Comment audit not found.");

            var replyText = !string.IsNullOrWhiteSpace(req?.ReplyText)
                ? req.ReplyText.Trim()
                : audit.GeneratedReplyText;

            if (string.IsNullOrWhiteSpace(replyText))
            {
                return Results.BadRequest(new { error = "No reply text provided and audit has no generated reply." });
            }

            var post = audit.Post;
            var account = post.PlatformThread?.Account;
            if (account == null)
            {
                account = await db.Accounts.FirstOrDefaultAsync(
                    a => a.UserId == userId && a.Platform == post.Platform, ct);
            }

            if (account == null)
            {
                return Results.BadRequest(new { error = "Post has no associated Account for publishing." });
            }

            var adapter = adapterResolver.Resolve(post.Platform);
            if (adapter == null)
            {
                return Results.BadRequest(new { error = $"Platform '{post.Platform}' is not supported for replies." });
            }

            var commentItem = new PlatformCommentItem(
                Platform: audit.Platform,
                RemoteCommentId: audit.RemoteCommentId,
                RemoteParentId: audit.RemoteParentId,
                RemoteRootId: audit.RemoteRootId ?? post.RemoteId ?? audit.RemoteCommentId,
                RemoteAuthorHandle: audit.RemoteAuthorHandle,
                RemoteAuthorId: audit.RemoteAuthorId,
                Text: audit.CommentText,
                CreatedAt: audit.CommentCreatedAt,
                Depth: 1,
                ExtractedUrls: Array.Empty<string>(),
                ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
                PlatformMetadata: null);

            var publishResult = await adapter.PublishReplyAsync(post, account, commentItem, replyText, ct);
            if (publishResult.Success)
            {
                audit.RankingDecision = "Replied";
                audit.GeneratedReplyText = replyText;
                audit.RemoteReplyId = publishResult.RemoteReplyId;
                audit.ErrorMessage = null;
                await db.SaveChangesAsync(ct);

                return Results.Ok(new ApproveCommentReplyResponse(
                    Success: true,
                    RemoteReplyId: publishResult.RemoteReplyId,
                    ReplyUrl: publishResult.RemoteReplyUrl,
                    ReplyText: replyText
                ));
            }
            else
            {
                audit.ErrorMessage = publishResult.ErrorMessage;
                await db.SaveChangesAsync(ct);

                return Results.BadRequest(new ApproveCommentReplyResponse(
                    Success: false,
                    RemoteReplyId: null,
                    ReplyUrl: null,
                    ReplyText: replyText,
                    ErrorMessage: publishResult.ErrorMessage
                ));
            }
        });
    }
}
