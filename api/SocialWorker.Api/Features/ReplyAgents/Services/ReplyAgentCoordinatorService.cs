using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Services;

public sealed class ReplyAgentCoordinatorService
{
    private readonly AppDbContext _db;
    private readonly IReplyPlatformAdapterResolver _adapterResolver;
    private readonly ICommentEnrichmentService _enrichmentService;
    private readonly ICommentRankingService _rankingService;
    private readonly IReplyGenerationService _replyGenerator;
    private readonly ICommentThreadSummarizer _summarizer;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<ReplyAgentCoordinatorService> _logger;

    public ReplyAgentCoordinatorService(
        AppDbContext db,
        IReplyPlatformAdapterResolver adapterResolver,
        ICommentEnrichmentService enrichmentService,
        ICommentRankingService rankingService,
        IReplyGenerationService replyGenerator,
        ICommentThreadSummarizer summarizer,
        IBackgroundTaskQueue taskQueue,
        ILogger<ReplyAgentCoordinatorService> logger)
    {
        _db = db;
        _adapterResolver = adapterResolver;
        _enrichmentService = enrichmentService;
        _rankingService = rankingService;
        _replyGenerator = replyGenerator;
        _summarizer = summarizer;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    public async Task ProcessPostCommentsAsync(
        Guid replyAgentId,
        Guid postId,
        CancellationToken ct = default)
    {
        var agent = await _db.ReplyAgents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == replyAgentId, ct);

        if (agent == null || !agent.IsEnabled)
        {
            return;
        }

        var post = await _db.Posts
            .Include(p => p.PlatformThread)
            .ThenInclude(pt => pt.Account)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);

        if (post == null || string.IsNullOrWhiteSpace(post.RemoteId))
        {
            return;
        }

        var account = post.PlatformThread?.Account;
        if (account == null)
        {
            var userId = agent.UserId;
            account = await _db.Accounts.FirstOrDefaultAsync(
                a => a.UserId == userId && a.Platform == post.Platform, ct);

            if (account != null && post.PlatformThread != null && post.PlatformThread.AccountId == null)
            {
                post.PlatformThread.AccountId = account.Id;
            }
        }

        if (account == null)
        {
            _logger.LogWarning("Post {PostId} has no associated Account for reply monitoring", post.Id);
            return;
        }

        var adapter = _adapterResolver.Resolve(post.Platform);
        if (adapter == null)
        {
            _logger.LogInformation("Platform {Platform} is not supported for reply monitoring", post.Platform);
            return;
        }

        var since = DateTime.UtcNow.Subtract(TimeSpan.FromHours(agent.MaxCommentAgeHours));
        var comments = await adapter.FetchCommentsAsync(post, account, since, ct);
        if (comments.Count == 0)
        {
            return;
        }

        var user = agent.User;

        foreach (var comment in comments.OrderBy(c => c.CreatedAt))
        {
            await ProcessSingleCommentAsync(agent, post, account, user, adapter, comment, since, ct);
        }

        agent.LastPolledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ProcessSingleCommentAsync(
        ReplyAgent agent,
        Post post,
        Account account,
        AppUser user,
        IReplyPlatformAdapter adapter,
        PlatformCommentItem comment,
        DateTime since,
        CancellationToken ct)
    {
        var alreadyAudited = await _db.WorkflowCommentAudits
            .AnyAsync(a => a.ReplyAgentId == agent.Id && a.RemoteCommentId == comment.RemoteCommentId, ct);
        if (alreadyAudited)
        {
            return;
        }

        if (string.Equals(comment.RemoteAuthorHandle, account.Handle, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(account.ExternalId) && string.Equals(comment.RemoteAuthorId, account.ExternalId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var branchKey = comment.RemoteParentId ?? comment.RemoteRootId;
        var thread = await _db.WorkflowCommentThreads
            .FirstOrDefaultAsync(t => t.ReplyAgentId == agent.Id && t.RemoteBranchKey == branchKey, ct);

        if (thread == null)
        {
            thread = WorkflowCommentThread.Create(agent.Id, post.Id, comment.RemoteRootId, branchKey);
            _db.WorkflowCommentThreads.Add(thread);
            await _db.SaveChangesAsync(ct);
        }

        if (comment.CreatedAt < since)
        {
            await RecordAuditAsync(agent, post, comment, 0, "TooOld", "SkippedTooOld", "Comment timestamp exceeds MaxCommentAgeHours.", ct);
            return;
        }

        if (thread.IsTerminal)
        {
            await RecordAuditAsync(agent, post, comment, 0, "Terminal", "SkippedTerminal", $"Thread previously marked terminal: {thread.TerminalReason}", ct);
            return;
        }

        if (thread.AgentReplyCount >= agent.MaxRepliesPerCommentThread)
        {
            await RecordAuditAsync(agent, post, comment, 0, "LimitReached", "SkippedStopCriteria", "MaxRepliesPerCommentThread exceeded for this branch.", ct);
            return;
        }

        if (comment.Depth > agent.MaxThreadDepth)
        {
            await RecordAuditAsync(agent, post, comment, 0, "DeepThread", "SkippedStopCriteria", "MaxThreadDepth exceeded for this conversation tree.", ct);
            return;
        }

        var userRepliesCount = await _db.WorkflowCommentAudits
            .CountAsync(a => a.ReplyAgentId == agent.Id && a.PostId == post.Id && a.RemoteAuthorId == comment.RemoteAuthorId && a.RankingDecision == "Replied", ct);
        if (userRepliesCount >= agent.MaxRepliesPerUser)
        {
            await RecordAuditAsync(agent, post, comment, 0, "UserLimitReached", "SkippedStopCriteria", "MaxRepliesPerUser exceeded for this user on this post.", ct);
            return;
        }

        var enrichedContext = await _enrichmentService.EnrichCommentAsync(comment, user, ct);

        var ranking = await _rankingService.RankCommentAsync(
            enrichedContext,
            post.PublishedText ?? "",
            thread.Summary,
            user,
            ct);

        if (ranking.IsTerminal)
        {
            thread.MarkTerminal("SentimentClosed");
            await _db.SaveChangesAsync(ct);
        }

        if (!ranking.ShouldReply || ranking.Score < agent.MinRankingScore)
        {
            var decision = ranking.IsTerminal ? "SkippedTerminal" : "SkippedLowScore";
            await RecordAuditWithEnrichmentAsync(agent, post, comment, enrichedContext, ranking.Score, ranking.Category, decision, ranking.Reasoning, null, null, null, ct);
            return;
        }

        var replyGen = await _replyGenerator.GenerateReplyAsync(
            enrichedContext,
            post.PublishedText ?? "",
            agent.PersonaPrompt,
            post.Platform,
            thread.Summary,
            user,
            ct);

        if (!replyGen.Success || string.IsNullOrWhiteSpace(replyGen.ReplyText))
        {
            await RecordAuditWithEnrichmentAsync(agent, post, comment, enrichedContext, ranking.Score, ranking.Category, "Failed", ranking.Reasoning, null, null, replyGen.ErrorMessage, ct);
            return;
        }

        var replyText = replyGen.ReplyText;

        if (!agent.AutoPublishReplies)
        {
            await RecordAuditWithEnrichmentAsync(agent, post, comment, enrichedContext, ranking.Score, ranking.Category, "PendingApproval", ranking.Reasoning, replyText, null, null, ct);
            return;
        }

        var pubResult = await adapter.PublishReplyAsync(post, account, comment, replyText, ct);
        if (pubResult.Success)
        {
            thread.IncrementAgentReplyCount();
            await RecordAuditWithEnrichmentAsync(agent, post, comment, enrichedContext, ranking.Score, ranking.Category, "Replied", ranking.Reasoning, replyText, pubResult.RemoteReplyId, null, ct);
            await _db.SaveChangesAsync(ct);

            await CheckAndCompactThreadAsync(thread, user, ct);
        }
        else
        {
            await RecordAuditWithEnrichmentAsync(agent, post, comment, enrichedContext, ranking.Score, ranking.Category, "Failed", ranking.Reasoning, replyText, null, pubResult.ErrorMessage, ct);
        }
    }

    private async Task CheckAndCompactThreadAsync(
        WorkflowCommentThread thread,
        AppUser user,
        CancellationToken ct)
    {
        try
        {
            var turnsSinceSummary = thread.AgentReplyCount * 2 - thread.SummarizedTurnCount;
            if (turnsSinceSummary >= 4)
            {
                var payload = new ReplyThreadSummaryPayload(thread.Id, user.Id);
                await _taskQueue.EnqueueAsync("reply-thread-summary", payload, null, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue thread compaction for thread {ThreadId}", thread.Id);
        }
    }

    private async Task RecordAuditAsync(
        ReplyAgent agent,
        Post post,
        PlatformCommentItem comment,
        int score,
        string category,
        string decision,
        string reasoning,
        CancellationToken ct)
    {
        var audit = WorkflowCommentAudit.Create(
            agent.Id,
            post.Id,
            comment.Platform,
            comment.RemoteCommentId,
            comment.RemoteAuthorHandle,
            comment.RemoteAuthorId,
            comment.Text,
            comment.CreatedAt,
            score,
            category,
            decision,
            reasoning,
            remoteParentId: comment.RemoteParentId,
            remoteRootId: comment.RemoteRootId);

        _db.WorkflowCommentAudits.Add(audit);
        await _db.SaveChangesAsync(ct);
    }

    private async Task RecordAuditWithEnrichmentAsync(
        ReplyAgent agent,
        Post post,
        PlatformCommentItem comment,
        EnrichedCommentContext enrichedContext,
        int score,
        string category,
        string decision,
        string? reasoning,
        string? replyText,
        string? remoteReplyId,
        string? error,
        CancellationToken ct)
    {
        var linksJson = enrichedContext.Links.Count > 0 ? JsonSerializer.Serialize(enrichedContext.Links) : null;
        var imagesJson = enrichedContext.Images.Count > 0 ? JsonSerializer.Serialize(enrichedContext.Images) : null;

        var audit = WorkflowCommentAudit.Create(
            agent.Id,
            post.Id,
            comment.Platform,
            comment.RemoteCommentId,
            comment.RemoteAuthorHandle,
            comment.RemoteAuthorId,
            comment.Text,
            comment.CreatedAt,
            score,
            category,
            decision,
            reasoning,
            replyText,
            comment.RemoteParentId,
            comment.RemoteRootId,
            linksJson,
            imagesJson,
            remoteReplyId,
            null,
            error);

        _db.WorkflowCommentAudits.Add(audit);
        await _db.SaveChangesAsync(ct);
    }
}
