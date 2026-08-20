using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedOrchestrationService
{
    private readonly FeedSourceIngestionService _sourceIngestionService;
    private readonly FeedThreadGenerationService _threadGenerationService;
    private readonly FeedAutoPublishService _autoPublishService;

    public FeedOrchestrationService(
        FeedSourceIngestionService sourceIngestionService,
        FeedThreadGenerationService threadGenerationService,
        FeedAutoPublishService autoPublishService)
    {
        _sourceIngestionService = sourceIngestionService;
        _threadGenerationService = threadGenerationService;
        _autoPublishService = autoPublishService;
    }

    public async Task ProcessFeedItemAsync(
        FeedSubscription subscription,
        AutomationWorkflow workflow,
        string itemTitle,
        string itemLink,
        string itemDescription,
        DateTime? itemPublishDate,
        CancellationToken ct)
    {
        var ingestionResult = await _sourceIngestionService.IngestSourceAsync(
            subscription,
            workflow,
            itemTitle,
            itemLink,
            itemDescription,
            ct);

        if (ingestionResult.Status != IngestionStatus.Success || ingestionResult.Draft == null)
        {
            return;
        }

        var prompt = !string.IsNullOrWhiteSpace(workflow.InstructionPrompt)
            ? workflow.InstructionPrompt
            : "Summarize this article as a thread.";

        var generationSuccess = await _threadGenerationService.GenerateThreadAsync(
            ingestionResult.Draft,
            itemLink,
            prompt,
            subscription.UserId,
            ct);

        if (!generationSuccess)
        {
            return;
        }

        var autoPublish = workflow.Destinations.Any(d => d.AutoPublish);

        await _autoPublishService.PublishIfEnabledAsync(
            ingestionResult.Draft,
            autoPublish,
            subscription.UserId,
            ct);
    }
}
