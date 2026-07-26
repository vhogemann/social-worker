using System;
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
        string itemTitle,
        string itemLink,
        string itemDescription,
        DateTime? itemPublishDate,
        CancellationToken ct)
    {
        var ingestionResult = await _sourceIngestionService.IngestSourceAsync(
            subscription,
            itemTitle,
            itemLink,
            itemDescription,
            ct);

        if (ingestionResult.Status != IngestionStatus.Success || ingestionResult.Draft == null)
        {
            return;
        }

        var generationSuccess = await _threadGenerationService.GenerateThreadAsync(
            ingestionResult.Draft,
            itemLink,
            subscription.InstructionPrompt,
            subscription.UserId,
            ct);

        if (!generationSuccess)
        {
            return;
        }

        await _autoPublishService.PublishIfEnabledAsync(
            ingestionResult.Draft,
            subscription.AutoPublish,
            subscription.UserId,
            ct);
    }
}
