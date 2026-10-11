using System;

namespace SocialWorker.Api.Data.Entities;

public class WorkflowCommentAudit
{
    public Guid Id { get; set; }
    public Guid WorkflowReplyAgentId { get; set; }
    public WorkflowReplyAgent WorkflowReplyAgent { get; set; } = null!;
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public string Platform { get; set; } = "Bluesky";
    public string RemoteCommentId { get; set; } = string.Empty;
    public string? RemoteParentId { get; set; }
    public string? RemoteRootId { get; set; }
    public string RemoteAuthorHandle { get; set; } = string.Empty;
    public string RemoteAuthorId { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public string? ParsedLinksJson { get; set; }
    public string? ParsedImagesJson { get; set; }
    public DateTime CommentCreatedAt { get; set; }
    public int RankingScore { get; set; }
    public string RankingCategory { get; set; } = "Unknown";
    public string RankingDecision { get; set; } = string.Empty;
    public string? RankingReasoning { get; set; }
    public string? GeneratedReplyText { get; set; }
    public string? RemoteReplyId { get; set; }
    public Guid? PublishedPostId { get; set; }
    public Post? PublishedPost { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static WorkflowCommentAudit Create(
        Guid workflowReplyAgentId,
        Guid postId,
        string platform,
        string remoteCommentId,
        string remoteAuthorHandle,
        string remoteAuthorId,
        string commentText,
        DateTime commentCreatedAt,
        int rankingScore,
        string rankingCategory,
        string rankingDecision,
        string? rankingReasoning = null,
        string? generatedReplyText = null,
        string? remoteParentId = null,
        string? remoteRootId = null,
        string? parsedLinksJson = null,
        string? parsedImagesJson = null,
        string? remoteReplyId = null,
        Guid? publishedPostId = null,
        string? errorMessage = null)
    {
        return new WorkflowCommentAudit
        {
            Id = Guid.NewGuid(),
            WorkflowReplyAgentId = workflowReplyAgentId,
            PostId = postId,
            Platform = string.IsNullOrWhiteSpace(platform) ? "Bluesky" : platform,
            RemoteCommentId = remoteCommentId,
            RemoteAuthorHandle = remoteAuthorHandle,
            RemoteAuthorId = remoteAuthorId,
            CommentText = commentText,
            CommentCreatedAt = commentCreatedAt,
            RankingScore = rankingScore,
            RankingCategory = rankingCategory,
            RankingDecision = rankingDecision,
            RankingReasoning = rankingReasoning,
            GeneratedReplyText = generatedReplyText,
            RemoteParentId = remoteParentId,
            RemoteRootId = remoteRootId,
            ParsedLinksJson = parsedLinksJson,
            ParsedImagesJson = parsedImagesJson,
            RemoteReplyId = remoteReplyId,
            PublishedPostId = publishedPostId,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };
    }
}
