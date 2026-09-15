using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class AutomationWorkflowSource
{
    public Guid Id { get; set; }
    public Guid AutomationWorkflowId { get; set; }
    public AutomationWorkflow AutomationWorkflow { get; set; } = null!;
    public WorkflowSourceKind Kind { get; set; } = WorkflowSourceKind.Feed;
    public Guid? FeedSubscriptionId { get; set; }
    public FeedSubscription? FeedSubscription { get; set; }
    public Guid? SourceWorkflowId { get; set; }
    public AutomationWorkflow? SourceWorkflow { get; set; }
    public string? ConfigurationJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static AutomationWorkflowSource CreateFeedSource(Guid automationWorkflowId, Guid feedSubscriptionId)
    {
        return new AutomationWorkflowSource
        {
            Id = Guid.NewGuid(),
            AutomationWorkflowId = automationWorkflowId,
            Kind = WorkflowSourceKind.Feed,
            FeedSubscriptionId = feedSubscriptionId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AutomationWorkflowSource CreateWorkflowHighlightsSource(Guid automationWorkflowId, Guid sourceWorkflowId, string? configurationJson = null)
    {
        return new AutomationWorkflowSource
        {
            Id = Guid.NewGuid(),
            AutomationWorkflowId = automationWorkflowId,
            Kind = WorkflowSourceKind.WorkflowHighlights,
            SourceWorkflowId = sourceWorkflowId,
            ConfigurationJson = configurationJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AutomationWorkflowSource Create(Guid automationWorkflowId, Guid feedSubscriptionId)
    {
        return CreateFeedSource(automationWorkflowId, feedSubscriptionId);
    }
}
