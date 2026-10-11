using System;
using System.Collections.Generic;

namespace SocialWorker.Api.Data.Entities;

public class AutomationWorkflow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string InstructionPrompt { get; set; } = null!;
    public Guid? ScheduleId { get; set; }
    public Schedule? Schedule { get; set; }
    public ICollection<AutomationWorkflowSource> WorkflowSources { get; set; } = new List<AutomationWorkflowSource>();
    public ICollection<AutomationWorkflowDestination> Destinations { get; set; } = new List<AutomationWorkflowDestination>();
    public ICollection<Draft> Drafts { get; set; } = new List<Draft>();
    public Guid? ReplyAgentId { get; set; }
    public ReplyAgent? ReplyAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static AutomationWorkflow Create(Guid userId, string title, string? instructionPrompt = null, Guid? scheduleId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Workflow title is required.", nameof(title));

        return new AutomationWorkflow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            InstructionPrompt = string.IsNullOrWhiteSpace(instructionPrompt)
                ? "Summarize this article as a thread."
                : instructionPrompt.Trim(),
            ScheduleId = scheduleId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
