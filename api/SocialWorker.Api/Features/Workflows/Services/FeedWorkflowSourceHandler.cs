using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Abstractions;

namespace SocialWorker.Api.Features.Workflows.Services;

public class FeedWorkflowSourceHandler : IWorkflowSourceHandler
{
    private readonly FeedPollingService _pollingService;
    private readonly ILogger<FeedWorkflowSourceHandler>? _logger;

    public FeedWorkflowSourceHandler(
        FeedPollingService pollingService,
        ILogger<FeedWorkflowSourceHandler>? logger = null)
    {
        _pollingService = pollingService;
        _logger = logger;
    }

    public WorkflowSourceKind Kind => WorkflowSourceKind.Feed;

    public async Task<int> ProcessAsync(AutomationWorkflow workflow, AutomationWorkflowSource source, CancellationToken ct)
    {
        if (source.FeedSubscriptionId.HasValue)
        {
            try
            {
                await _pollingService.PollSubscriptionAsync(source.FeedSubscriptionId.Value, ct, forceAll: true);
                return 1;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to poll feed subscription {FeedSubscriptionId} for workflow {WorkflowId}", source.FeedSubscriptionId.Value, workflow.Id);
                return 0;
            }
        }

        return 0;
    }
}
