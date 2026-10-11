using System;
using System.Collections.Generic;

namespace SocialWorker.Api.Data.Entities;

public class ReplyAgent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string PersonaPrompt { get; set; } = string.Empty;
    public int MaxCommentAgeHours { get; set; } = 24;
    public int MaxPostAgeDays { get; set; } = 7;
    public int MinRankingScore { get; set; } = 7;
    public int MaxRepliesPerCommentThread { get; set; } = 2;
    public int MaxRepliesPerUser { get; set; } = 3;
    public int MaxThreadDepth { get; set; } = 4;
    public bool AutoPublishReplies { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 15;
    public DateTime? LastPolledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AutomationWorkflow> Workflows { get; set; } = new List<AutomationWorkflow>();
    public ICollection<WorkflowCommentThread> CommentThreads { get; set; } = new List<WorkflowCommentThread>();
    public ICollection<WorkflowCommentAudit> CommentAudits { get; set; } = new List<WorkflowCommentAudit>();

    public static ReplyAgent Create(
        Guid userId,
        string name,
        string personaPrompt,
        string? description = null,
        int maxCommentAgeHours = 24,
        int maxPostAgeDays = 7,
        int minRankingScore = 7,
        int maxRepliesPerCommentThread = 2,
        int maxRepliesPerUser = 3,
        int maxThreadDepth = 4,
        bool autoPublishReplies = true,
        int checkIntervalMinutes = 15)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return new ReplyAgent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsEnabled = true,
            PersonaPrompt = string.IsNullOrWhiteSpace(personaPrompt)
                ? "You are a helpful and polite social media assistant. Respond concisely to questions and feedback."
                : personaPrompt.Trim(),
            MaxCommentAgeHours = Math.Max(1, maxCommentAgeHours),
            MaxPostAgeDays = Math.Max(1, maxPostAgeDays),
            MinRankingScore = Math.Clamp(minRankingScore, 1, 10),
            MaxRepliesPerCommentThread = Math.Max(1, maxRepliesPerCommentThread),
            MaxRepliesPerUser = Math.Max(1, maxRepliesPerUser),
            MaxThreadDepth = Math.Max(1, maxThreadDepth),
            AutoPublishReplies = autoPublishReplies,
            CheckIntervalMinutes = Math.Max(1, checkIntervalMinutes),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
