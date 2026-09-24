using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Abstractions;

namespace SocialWorker.Api.Features.Workflows.Services;

public class FeedWorkflowSourceHandler : IWorkflowSourceHandler
{
    private readonly FeedPollingService _pollingService;

    public FeedWorkflowSourceHandler(FeedPollingService pollingService)
    {
        _pollingService = pollingService;
    }

    public WorkflowSourceKind Kind => WorkflowSourceKind.Feed;

    public async Task<int> ProcessAsync(AutomationWorkflow workflow, AutomationWorkflowSource source, CancellationToken ct)
    {
        if (source.FeedSubscriptionId.HasValue)
        {
            await _pollingService.PollSubscriptionAsync(source.FeedSubscriptionId.Value, ct, forceAll: true);
            return 1;
        }

        return 0;
    }
}
