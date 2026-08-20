using System;

namespace SocialWorker.Api.Data.Entities;

public class AutomationWorkflowSource
{
    public Guid AutomationWorkflowId { get; set; }
    public AutomationWorkflow AutomationWorkflow { get; set; } = null!;
    public Guid FeedSubscriptionId { get; set; }
    public FeedSubscription FeedSubscription { get; set; } = null!;

    public static AutomationWorkflowSource Create(Guid automationWorkflowId, Guid feedSubscriptionId)
    {
        return new AutomationWorkflowSource
        {
            AutomationWorkflowId = automationWorkflowId,
            FeedSubscriptionId = feedSubscriptionId
        };
    }
}
