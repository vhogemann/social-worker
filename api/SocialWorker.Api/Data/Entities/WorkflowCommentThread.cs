using System;

namespace SocialWorker.Api.Data.Entities;

public class WorkflowCommentThread
{
    public Guid Id { get; set; }
    public Guid ReplyAgentId { get; set; }
    public ReplyAgent ReplyAgent { get; set; } = null!;
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public string RemoteThreadKey { get; set; } = string.Empty;
    public string RemoteBranchKey { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SummarizedUpToRemoteId { get; set; }
    public int SummarizedTurnCount { get; set; }
    public int AgentReplyCount { get; set; }
    public bool IsTerminal { get; set; }
    public string? TerminalReason { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static WorkflowCommentThread Create(
        Guid replyAgentId,
        Guid postId,
        string remoteThreadKey,
        string remoteBranchKey)
    {
        if (replyAgentId == Guid.Empty)
            throw new ArgumentException("ReplyAgentId is required.", nameof(replyAgentId));
        if (postId == Guid.Empty)
            throw new ArgumentException("PostId is required.", nameof(postId));
        if (string.IsNullOrWhiteSpace(remoteThreadKey))
            throw new ArgumentException("RemoteThreadKey is required.", nameof(remoteThreadKey));
        if (string.IsNullOrWhiteSpace(remoteBranchKey))
            throw new ArgumentException("RemoteBranchKey is required.", nameof(remoteBranchKey));

        return new WorkflowCommentThread
        {
            Id = Guid.NewGuid(),
            ReplyAgentId = replyAgentId,
            PostId = postId,
            RemoteThreadKey = remoteThreadKey.Trim(),
            RemoteBranchKey = remoteBranchKey.Trim(),
            SummarizedTurnCount = 0,
            AgentReplyCount = 0,
            IsTerminal = false,
            LastActivityAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void MarkTerminal(string reason)
    {
        IsTerminal = true;
        TerminalReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementAgentReplyCount()
    {
        AgentReplyCount++;
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSummary(string summary, string summarizedUpToRemoteId, int turnsSummarized)
    {
        Summary = summary;
        SummarizedUpToRemoteId = summarizedUpToRemoteId;
        SummarizedTurnCount += turnsSummarized;
        UpdatedAt = DateTime.UtcNow;
    }
}
