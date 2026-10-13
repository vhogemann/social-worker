using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Services;

public sealed class ReplyAgentPostService
{
    private readonly AppDbContext _db;
    private readonly IExternalPostResolver _postResolver;
    private readonly IBackgroundTaskQueue _taskQueue;

    public ReplyAgentPostService(
        AppDbContext db,
        IExternalPostResolver postResolver,
        IBackgroundTaskQueue taskQueue)
    {
        _db = db;
        _postResolver = postResolver;
        _taskQueue = taskQueue;
    }

    public async Task<AssignReplyAgentPostResult> AssignPostAsync(
        Guid userId,
        Guid replyAgentId,
        AssignReplyAgentPostRequest req,
        CancellationToken ct = default)
    {
        var agent = await _db.ReplyAgents
            .FirstOrDefaultAsync(a => a.Id == replyAgentId && a.UserId == userId, ct);

        if (agent == null)
            return AssignReplyAgentPostResult.Fail("Reply agent not found.");

        Post? post = null;

        if (req.PostId.HasValue)
        {
            post = await _db.Posts
                .Include(p => p.Draft)
                .FirstOrDefaultAsync(p => p.Id == req.PostId.Value && (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)), ct);

            if (post == null)
                return AssignReplyAgentPostResult.Fail("Specified post was not found.");
        }
        else if (!string.IsNullOrWhiteSpace(req.PostUrl) || !string.IsNullOrWhiteSpace(req.RemoteId))
        {
            var target = !string.IsNullOrWhiteSpace(req.PostUrl) ? req.PostUrl : req.RemoteId!;
            var resolution = await _postResolver.ResolveOrCreatePostAsync(userId, target, req.Platform, ct);
            if (!resolution.Success || resolution.Post == null)
            {
                return AssignReplyAgentPostResult.Fail(resolution.Error ?? "Failed to resolve post.");
            }
            post = resolution.Post;
        }
        else
        {
            return AssignReplyAgentPostResult.Fail("Either PostId or PostUrl is required.");
        }

        var existingAssignment = await _db.ReplyAgentPosts
            .FirstOrDefaultAsync(rap => rap.ReplyAgentId == agent.Id && rap.PostId == post.Id, ct);

        if (existingAssignment == null)
        {
            existingAssignment = ReplyAgentPost.Create(agent.Id, post.Id);
            _db.ReplyAgentPosts.Add(existingAssignment);
            await _db.SaveChangesAsync(ct);
        }
        else if (!existingAssignment.IsActive)
        {
            existingAssignment.IsActive = true;
            await _db.SaveChangesAsync(ct);
        }

        var dto = new ReplyAgentAssignedPostDto(
            PostId: post.Id,
            ReplyAgentId: agent.Id,
            AgentName: agent.Name,
            Platform: post.Platform,
            RemoteId: post.RemoteId,
            Url: post.Url,
            AuthorHandle: post.AuthorHandle,
            PublishedText: post.PublishedText,
            IsExternal: post.IsExternal,
            IsActive: existingAssignment.IsActive,
            AssignedAt: existingAssignment.AssignedAt,
            LastPolledAt: existingAssignment.LastPolledAt,
            CommentsAuditedCount: 0,
            RepliesSentCount: 0
        );

        return AssignReplyAgentPostResult.Ok(dto);
    }

    public async Task<bool> UnassignPostAsync(
        Guid userId,
        Guid replyAgentId,
        Guid postId,
        CancellationToken ct = default)
    {
        var assignment = await _db.ReplyAgentPosts
            .Include(rap => rap.ReplyAgent)
            .FirstOrDefaultAsync(rap => rap.ReplyAgentId == replyAgentId && rap.PostId == postId && rap.ReplyAgent.UserId == userId, ct);

        if (assignment == null)
            return false;

        _db.ReplyAgentPosts.Remove(assignment);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ReplyAgentAssignedPostDto>> GetAssignedPostsAsync(
        Guid userId,
        Guid? replyAgentId = null,
        CancellationToken ct = default)
    {
        var query = _db.ReplyAgentPosts
            .Include(rap => rap.ReplyAgent)
            .Include(rap => rap.Post)
            .Where(rap => rap.ReplyAgent.UserId == userId);

        if (replyAgentId.HasValue)
        {
            query = query.Where(rap => rap.ReplyAgentId == replyAgentId.Value);
        }

        var assignments = await query
            .OrderByDescending(rap => rap.AssignedAt)
            .ToListAsync(ct);

        var postIds = assignments.Select(a => a.PostId).Distinct().ToList();
        var audits = await _db.WorkflowCommentAudits
            .Where(a => postIds.Contains(a.PostId))
            .GroupBy(a => a.PostId)
            .Select(g => new
            {
                PostId = g.Key,
                TotalAudits = g.Count(),
                RepliesSent = g.Count(x => x.RankingDecision == "Replied")
            })
            .ToDictionaryAsync(x => x.PostId, ct);

        return assignments.Select(rap =>
        {
            audits.TryGetValue(rap.PostId, out var stat);
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
                CommentsAuditedCount: stat?.TotalAudits ?? 0,
                RepliesSentCount: stat?.RepliesSent ?? 0
            );
        }).ToList();
    }

    public async Task<bool> PollAssignedPostAsync(
        Guid userId,
        Guid replyAgentId,
        Guid postId,
        CancellationToken ct = default)
    {
        var assignment = await _db.ReplyAgentPosts
            .Include(rap => rap.ReplyAgent)
            .Include(rap => rap.Post)
            .FirstOrDefaultAsync(rap => rap.ReplyAgentId == replyAgentId && rap.PostId == postId && rap.ReplyAgent.UserId == userId, ct);

        if (assignment == null)
            return false;

        var dedupKey = $"reply-check-{replyAgentId}-{postId}-{DateTime.UtcNow:yyyyMMddHHmm}";
        var payload = new WorkflowReplyCheckPostPayload(replyAgentId, postId);
        var options = EnqueueTaskOptions.WithDeduplication(dedupKey);

        await _taskQueue.EnqueueAsync("workflow-reply-check-post", payload, options, ct);

        assignment.LastPolledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
