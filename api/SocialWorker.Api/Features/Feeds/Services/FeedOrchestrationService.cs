using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Workflows.Services;

namespace SocialWorker.Api.Features.Feeds;

public class FeedOrchestrationService
{
    private readonly FeedSourceIngestionService _sourceIngestionService;
    private readonly FeedThreadGenerationService _threadGenerationService;
    private readonly DraftValidationService _validationService;
    private readonly FeedAutoPublishService _autoPublishService;
    private readonly ILogger<FeedOrchestrationService> _logger;

    public FeedOrchestrationService(
        FeedSourceIngestionService sourceIngestionService,
        FeedThreadGenerationService threadGenerationService,
        DraftValidationService validationService,
        FeedAutoPublishService autoPublishService,
        ILogger<FeedOrchestrationService> logger)
    {
        _sourceIngestionService = sourceIngestionService;
        _threadGenerationService = threadGenerationService;
        _validationService = validationService;
        _autoPublishService = autoPublishService;
        _logger = logger;
    }

    public virtual async Task ProcessFeedItemAsync(
        FeedSubscription subscription,
        AutomationWorkflow workflow,
        string itemTitle,
        string itemLink,
        string itemDescription,
        DateTime? itemPublishDate,
        CancellationToken ct)
    {
        // 1. Ingest platform-agnostic canonical draft
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

        var canonicalDraft = ingestionResult.Draft;
        var prompt = !string.IsNullOrWhiteSpace(workflow.InstructionPrompt)
            ? workflow.InstructionPrompt
            : "Summarize this article as a thread.";

        // 2. Generate canonical thread with self-correction loop (up to 2 retries)
        var generationAttempts = 0;
        const int maxAttempts = 3;
        var isValidCanonical = false;

        while (generationAttempts < maxAttempts)
        {
            generationAttempts++;
            var success = await _threadGenerationService.GenerateCanonicalThreadAsync(
                canonicalDraft,
                itemLink,
                prompt,
                subscription.UserId,
                ct);

            if (!success)
            {
                _logger.LogWarning("Generation attempt {Attempt} failed for canonical draft {DraftId}", generationAttempts, canonicalDraft.Id);
                continue;
            }

            var validationResult = await _validationService.ValidateDraftAsync(canonicalDraft, null, ct);
            if (validationResult.IsValid)
            {
                isValidCanonical = true;
                break;
            }

            _logger.LogWarning("Canonical draft {DraftId} validation rejected on attempt {Attempt}: {Feedback}",
                canonicalDraft.Id, generationAttempts, validationResult.Feedback);
        }

        if (!isValidCanonical)
        {
            _logger.LogError("Canonical draft {DraftId} failed all {MaxAttempts} generation/validation attempts.", canonicalDraft.Id, maxAttempts);
            canonicalDraft.Status = DraftStatus.Failed;
            return;
        }

        // 3. Generate platform-specific variants for each workflow destination
        if (workflow.Destinations != null && workflow.Destinations.Count > 0)
        {
            foreach (var destination in workflow.Destinations)
            {
                var variant = await _threadGenerationService.GeneratePlatformVariantDraftAsync(
                    canonicalDraft,
                    destination,
                    subscription.UserId,
                    ct);

                if (variant != null)
                {
                    await _validationService.ValidateDraftAsync(variant, destination.Platform, ct);
                }
            }

            // 4. Auto-publish enabled destinations
            await _autoPublishService.PublishDestinationsAsync(
                canonicalDraft,
                workflow.Destinations,
                subscription.UserId,
                ct);
        }
    }
}
