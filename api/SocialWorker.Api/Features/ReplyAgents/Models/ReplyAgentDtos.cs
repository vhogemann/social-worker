using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record ReplyAgentSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    int CheckIntervalMinutes,
    int AssignedWorkflowsCount,
    DateTime? LastPolledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record ReplyAgentDto(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
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
    int AssignedWorkflowsCount,
    int AssignedPostsCount,
    IReadOnlyList<ReplyAgentWorkflowSummaryDto> Workflows,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public ReplyAgentDto(ReplyAgent a, IReadOnlyList<ReplyAgentWorkflowSummaryDto>? workflows = null, int? assignedPostsCount = null) : this(
        a.Id,
        a.UserId,
        a.Name,
        a.Description,
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
        workflows?.Count ?? a.Workflows?.Count ?? 0,
        assignedPostsCount ?? a.AssignedPosts?.Count ?? 0,
        workflows ?? (a.Workflows != null ? a.Workflows.Select(w => new ReplyAgentWorkflowSummaryDto(w.Id, w.Title)).ToList() : Array.Empty<ReplyAgentWorkflowSummaryDto>()),
        a.CreatedAt,
        a.UpdatedAt
    ) { }
}

public sealed record ReplyAgentWorkflowSummaryDto(Guid Id, string Title);

public sealed record AssignReplyAgentPostRequest(
    Guid? PostId = null,
    string? PostUrl = null,
    string? Platform = null,
    string? RemoteId = null
);

public sealed record ReplyAgentAssignedPostDto(
    Guid PostId,
    Guid ReplyAgentId,
    string AgentName,
    string Platform,
    string? RemoteId,
    string? Url,
    string? AuthorHandle,
    string? PublishedText,
    bool IsExternal,
    bool IsActive,
    DateTime AssignedAt,
    DateTime? LastPolledAt,
    int CommentsAuditedCount = 0,
    int RepliesSentCount = 0
);

public sealed record AssignReplyAgentPostResult(
    bool Success,
    ReplyAgentAssignedPostDto? AssignedPost,
    string? Error = null)
{
    public static AssignReplyAgentPostResult Ok(ReplyAgentAssignedPostDto post) => new(true, post, null);
    public static AssignReplyAgentPostResult Fail(string error) => new(false, null, error);
}

public sealed record CreateReplyAgentRequest(
    string Name,
    string? Description = null,
    bool IsEnabled = true,
    string PersonaPrompt = "",
    int MaxCommentAgeHours = 24,
    int MaxPostAgeDays = 7,
    int MinRankingScore = 7,
    int MaxRepliesPerCommentThread = 2,
    int MaxRepliesPerUser = 3,
    int MaxThreadDepth = 4,
    bool AutoPublishReplies = true,
    int CheckIntervalMinutes = 15
);

public sealed record UpdateReplyAgentRequest(
    string Name,
    string? Description = null,
    bool IsEnabled = true,
    string PersonaPrompt = "",
    int MaxCommentAgeHours = 24,
    int MaxPostAgeDays = 7,
    int MinRankingScore = 7,
    int MaxRepliesPerCommentThread = 2,
    int MaxRepliesPerUser = 3,
    int MaxThreadDepth = 4,
    bool AutoPublishReplies = true,
    int CheckIntervalMinutes = 15
);

public sealed record AssignWorkflowReplyAgentRequest(Guid? ReplyAgentId);

public sealed record WorkflowCommentAuditDto(
    Guid Id,
    Guid ReplyAgentId,
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
        a.ReplyAgentId,
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

public sealed record ApproveCommentReplyRequest(string? ReplyText = null);

public sealed record ApproveCommentReplyResponse(
    bool Success,
    string? RemoteReplyId,
    string? ReplyUrl,
    string ReplyText,
    string? ErrorMessage = null
);
