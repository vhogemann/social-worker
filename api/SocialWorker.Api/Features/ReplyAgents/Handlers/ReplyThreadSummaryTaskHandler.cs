using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Handlers;

public sealed class ReplyThreadSummaryTaskHandler : BackgroundTaskHandlerBase<ReplyThreadSummaryPayload>
{
    private readonly AppDbContext _db;
    private readonly ICommentThreadSummarizer _summarizer;
    private readonly ILogger<ReplyThreadSummaryTaskHandler> _logger;

    public override string TaskType => "reply-thread-summary";
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(3);
    public override int DefaultMaxAttempts => 2;

    public ReplyThreadSummaryTaskHandler(
        AppDbContext db,
        ICommentThreadSummarizer summarizer,
        ILogger<ReplyThreadSummaryTaskHandler> logger)
    {
        _db = db;
        _summarizer = summarizer;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        ReplyThreadSummaryPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var thread = await _db.WorkflowCommentThreads
            .FirstOrDefaultAsync(t => t.Id == payload.ThreadId, ct);

        if (thread == null) return TaskResult.Success();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == payload.UserId && u.IsActive, ct);

        if (user == null) return TaskResult.Fatal("User not found or inactive.");

        var audits = await _db.WorkflowCommentAudits
            .Where(a => a.ReplyAgentId == thread.ReplyAgentId && a.PostId == thread.PostId
                && (a.RemoteParentId == thread.RemoteBranchKey || a.RemoteCommentId == thread.RemoteBranchKey))
            .OrderBy(a => a.CommentCreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (audits.Count == 0) return TaskResult.Success();

        var commentItems = audits.Select(a => new PlatformCommentItem(
            Platform: a.Platform,
            RemoteCommentId: a.RemoteCommentId,
            RemoteParentId: a.RemoteParentId,
            RemoteRootId: a.RemoteRootId ?? "",
            RemoteAuthorHandle: a.RemoteAuthorHandle,
            RemoteAuthorId: a.RemoteAuthorId,
            Text: a.CommentText + (!string.IsNullOrWhiteSpace(a.GeneratedReplyText) ? $" -> Reply: {a.GeneratedReplyText}" : ""),
            CreatedAt: a.CommentCreatedAt,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        )).ToList();

        var updatedSummary = await _summarizer.CompactThreadAsync(thread.Summary, commentItems, user, ct);
        if (!string.IsNullOrWhiteSpace(updatedSummary))
        {
            var lastRemoteId = audits.Last().RemoteCommentId;
            thread.UpdateSummary(updatedSummary, lastRemoteId, audits.Count);
            await _db.SaveChangesAsync(ct);
        }

        return TaskResult.Success();
    }
}
