using System;

namespace SocialWorker.Api.Data.Entities;

public class AutomationWorkflowDestination
{
    public Guid Id { get; set; }
    public Guid AutomationWorkflowId { get; set; }
    public AutomationWorkflow AutomationWorkflow { get; set; } = null!;
    public string Platform { get; set; } = null!;
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }
    public bool AutoPublish { get; set; }

    public static AutomationWorkflowDestination Create(Guid automationWorkflowId, string platform, bool autoPublish = false, Guid? accountId = null)
    {
        if (string.IsNullOrWhiteSpace(platform))
            throw new ArgumentException("Platform is required.", nameof(platform));

        return new AutomationWorkflowDestination
        {
            Id = Guid.NewGuid(),
            AutomationWorkflowId = automationWorkflowId,
            Platform = platform.Trim(),
            AutoPublish = autoPublish,
            AccountId = accountId
        };
    }
}
