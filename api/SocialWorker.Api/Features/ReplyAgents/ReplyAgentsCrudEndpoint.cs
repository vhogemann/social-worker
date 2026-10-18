using System;
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
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.ReplyAgents;

public static class ReplyAgentsCrudEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reply-agents")
            .RequireAuthorization();

        group.MapGet("", async (
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agents = await db.ReplyAgents
                .Include(a => a.Workflows)
                .Include(a => a.AssignedPosts)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var result = agents.Select(a => new ReplyAgentDto(a)).ToList();
            return Results.Ok(result);
        });

        group.MapGet("/posts", async (
            Guid? agentId,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var query = db.ReplyAgentPosts
                .Include(rap => rap.ReplyAgent)
                .Include(rap => rap.Post)
                .Where(rap => rap.ReplyAgent.UserId == userId.Value);

            if (agentId.HasValue)
            {
                query = query.Where(rap => rap.ReplyAgentId == agentId.Value);
            }

            var list = await query
                .OrderByDescending(rap => rap.AssignedAt)
                .ToListAsync(ct);

            var postIds = list.Select(l => l.PostId).Distinct().ToList();
            var auditsSummary = await db.WorkflowCommentAudits
                .Where(a => postIds.Contains(a.PostId))
                .GroupBy(a => new { a.ReplyAgentId, a.PostId })
                .Select(g => new
                {
                    g.Key.ReplyAgentId,
                    g.Key.PostId,
                    AuditedCount = g.Count(),
                    RepliedCount = g.Count(x => x.RankingDecision == "Replied")
                })
                .ToListAsync(ct);

            var dtos = list.Select(rap =>
            {
                var summary = auditsSummary.FirstOrDefault(s => s.ReplyAgentId == rap.ReplyAgentId && s.PostId == rap.PostId);
                return new ReplyAgentAssignedPostDto(
                    PostId: rap.PostId,
                    ReplyAgentId: rap.ReplyAgentId,
                    AgentName: rap.ReplyAgent.Name,
                    Platform: rap.Post.Platform,
                    RemoteId: rap.Post.RemoteId,
                    Url: rap.Post.Url,
                    AuthorHandle: rap.Post.AuthorHandle,
                    PublishedText: rap.Post.PublishedText,
                    IsExternal: rap.Post.IsExternal,
                    IsActive: rap.IsActive,
                    AssignedAt: rap.AssignedAt,
                    LastPolledAt: rap.LastPolledAt,
                    CommentsAuditedCount: summary?.AuditedCount ?? 0,
                    RepliesSentCount: summary?.RepliedCount ?? 0
                );
            }).ToList();

            return Results.Ok(dtos);
        });

        group.MapGet("/audits", async (
            Guid? agentId,
            Guid? postId,
            string? decision,
            int? limit,
            int? offset,
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var query = db.WorkflowCommentAudits
                .Include(a => a.Post)
                .Include(a => a.ReplyAgent)
                .Where(a => a.ReplyAgent.UserId == userId.Value);

            if (agentId.HasValue)
            {
                query = query.Where(a => a.ReplyAgentId == agentId.Value);
            }

            if (postId.HasValue)
            {
                query = query.Where(a => a.PostId == postId.Value);
            }

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
                .ToListAsync(ct);

            return Results.Ok(audits.Select(a => new WorkflowCommentAuditDto(a)));
        });

        group.MapPost("/audits/{auditId:guid}/reply", async (
            Guid auditId,
            ApproveCommentReplyRequest? req,
            ClaimsPrincipal user,
            AppDbContext db,
            IReplyPlatformAdapterResolver adapterResolver,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var audit = await db.WorkflowCommentAudits
                .Include(a => a.ReplyAgent)
                .Include(a => a.Post)
                    .ThenInclude(p => p.PlatformThread)
                        .ThenInclude(pt => pt.Account)
                .FirstOrDefaultAsync(a => a.Id == auditId && a.ReplyAgent.UserId == userId.Value, ct);

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
                    a => a.UserId == userId.Value && a.Platform == post.Platform, ct);
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
                if (audit.RemoteLikeId == null)
                {
                    try
                    {
                        var likeResult = await adapter.LikeCommentAsync(post, account, commentItem, ct);
                        if (likeResult.Success)
                        {
                            audit.RemoteLikeId = likeResult.RemoteLikeId;
                        }
                    }
                    catch
                    {
                    }
                }

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

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agent = await db.ReplyAgents
                .Include(a => a.Workflows)
                .Include(a => a.AssignedPosts)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (agent == null) return Results.NotFound("Reply Agent not found.");

            return Results.Ok(new ReplyAgentDto(agent));
        });

        group.MapGet("/{agentId:guid}/posts", async (
            Guid agentId,
            ClaimsPrincipal user,
            ReplyAgentPostService postService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var posts = await postService.GetAssignedPostsAsync(userId.Value, agentId, ct);
            return Results.Ok(posts);
        });

        group.MapPost("/{agentId:guid}/posts", async (
            Guid agentId,
            AssignReplyAgentPostRequest req,
            ClaimsPrincipal user,
            ReplyAgentPostService postService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var result = await postService.AssignPostAsync(userId.Value, agentId, req, ct);
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(result.AssignedPost);
        });

        group.MapDelete("/{agentId:guid}/posts/{postId:guid}", async (
            Guid agentId,
            Guid postId,
            ClaimsPrincipal user,
            ReplyAgentPostService postService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var success = await postService.UnassignPostAsync(userId.Value, agentId, postId, ct);
            if (!success)
            {
                return Results.NotFound(new { error = "Post assignment not found." });
            }

            return Results.NoContent();
        });

        group.MapPost("/{agentId:guid}/posts/{postId:guid}/poll", async (
            Guid agentId,
            Guid postId,
            ClaimsPrincipal user,
            ReplyAgentPostService postService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var success = await postService.PollAssignedPostAsync(userId.Value, agentId, postId, ct);
            if (!success)
            {
                return Results.NotFound(new { error = "Active post assignment not found." });
            }

            return Results.Ok(new { success = true });
        });

        group.MapPost("", async (
            CreateReplyAgentRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Agent name is required.");
            }

            var agent = ReplyAgent.Create(
                userId.Value,
                req.Name,
                req.PersonaPrompt,
                req.Description,
                req.MaxCommentAgeHours,
                req.MaxPostAgeDays,
                req.MinRankingScore,
                req.MaxRepliesPerCommentThread,
                req.MaxRepliesPerUser,
                req.MaxThreadDepth,
                req.AutoPublishReplies,
                req.CheckIntervalMinutes);
            agent.IsEnabled = req.IsEnabled;

            db.ReplyAgents.Add(agent);
            await db.SaveChangesAsync();

            return Results.Created($"/api/reply-agents/{agent.Id}", new ReplyAgentDto(agent));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateReplyAgentRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agent = await db.ReplyAgents
                .Include(a => a.Workflows)
                .Include(a => a.AssignedPosts)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (agent == null) return Results.NotFound("Reply Agent not found.");

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Agent name is required.");
            }

            agent.Name = req.Name.Trim();
            agent.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
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

            await db.SaveChangesAsync();
            return Results.Ok(new ReplyAgentDto(agent));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agent = await db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (agent == null) return Results.NotFound("Reply Agent not found.");

            foreach (var wf in agent.Workflows)
            {
                wf.ReplyAgentId = null;
            }

            db.ReplyAgents.Remove(agent);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
