using System;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record WorkflowReplyAgentDto(
    Guid Id,
    Guid AutomationWorkflowId,
    bool IsEnabled,
    string PersonaPrompt,
    int MaxCommentAgeHours,
    int MaxPostAgeDays,
    int MinRankingScore,
    int MaxRepliesPerCommentThread,
    int MaxRepliesPerUser,
    int MaxThreadDepth,
    bool AutoPublishReplies,
    int CheckIntervalMinutes,
    DateTime? LastPolledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public WorkflowReplyAgentDto(WorkflowReplyAgent a) : this(
        a.Id,
        a.AutomationWorkflowId,
        a.IsEnabled,
        a.PersonaPrompt,
        a.MaxCommentAgeHours,
        a.MaxPostAgeDays,
        a.MinRankingScore,
        a.MaxRepliesPerCommentThread,
        a.MaxRepliesPerUser,
        a.MaxThreadDepth,
        a.AutoPublishReplies,
        a.CheckIntervalMinutes,
        a.LastPolledAt,
        a.CreatedAt,
        a.UpdatedAt
    ) { }
}

public sealed record UpdateWorkflowReplyAgentRequest(
    bool IsEnabled,
    string PersonaPrompt,
    int MaxCommentAgeHours = 24,
    int MaxPostAgeDays = 7,
    int MinRankingScore = 7,
    int MaxRepliesPerCommentThread = 2,
    int MaxRepliesPerUser = 3,
    int MaxThreadDepth = 4,
    bool AutoPublishReplies = true,
    int CheckIntervalMinutes = 15
);

public sealed record WorkflowCommentAuditDto(
    Guid Id,
    Guid WorkflowReplyAgentId,
    Guid PostId,
    string Platform,
    string RemoteCommentId,
    string? RemoteParentId,
    string? RemoteRootId,
    string RemoteAuthorHandle,
    string RemoteAuthorId,
    string CommentText,
    string? ParsedLinksJson,
    string? ParsedImagesJson,
    DateTime CommentCreatedAt,
    int RankingScore,
    string RankingCategory,
    string RankingDecision,
    string? RankingReasoning,
    string? GeneratedReplyText,
    string? RemoteReplyId,
    Guid? PublishedPostId,
    string? ErrorMessage,
    DateTime CreatedAt
)
{
    public WorkflowCommentAuditDto(WorkflowCommentAudit a) : this(
        a.Id,
        a.WorkflowReplyAgentId,
        a.PostId,
        a.Platform,
        a.RemoteCommentId,
        a.RemoteParentId,
        a.RemoteRootId,
        a.RemoteAuthorHandle,
        a.RemoteAuthorId,
        a.CommentText,
        a.ParsedLinksJson,
        a.ParsedImagesJson,
        a.CommentCreatedAt,
        a.RankingScore,
        a.RankingCategory,
        a.RankingDecision,
        a.RankingReasoning,
        a.GeneratedReplyText,
        a.RemoteReplyId,
        a.PublishedPostId,
        a.ErrorMessage,
        a.CreatedAt
    ) { }
}
