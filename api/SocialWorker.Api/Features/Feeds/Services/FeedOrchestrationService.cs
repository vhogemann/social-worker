using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Workflows.Services;

namespace SocialWorker.Api.Features.Feeds;

public class FeedOrchestrationService
{
    private readonly AppDbContext _db;
    private readonly FeedSourceIngestionService _sourceIngestionService;
    private readonly FeedThreadGenerationService _threadGenerationService;
    private readonly DraftValidationService _validationService;
    private readonly FeedAutoPublishService _autoPublishService;
    private readonly ILogger<FeedOrchestrationService> _logger;

    public FeedOrchestrationService(
        AppDbContext db,
        FeedSourceIngestionService sourceIngestionService,
        FeedThreadGenerationService threadGenerationService,
        DraftValidationService validationService,
        FeedAutoPublishService autoPublishService,
        ILogger<FeedOrchestrationService> logger)
    {
        _db = db;
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
        var ingestionResult = await _sourceIngestionService.IngestSourceAsync(
            subscription,
            workflow,
            itemTitle,
            itemLink,
            itemDescription,
            ct);

        if (ingestionResult.Status == IngestionStatus.PendingTranscription)
        {
            return;
        }

        if (ingestionResult.Status != IngestionStatus.Success || ingestionResult.Draft == null)
        {
            return;
        }

        await ProcessCanonicalDraftOrchestrationAsync(
            ingestionResult.Draft,
            workflow,
            itemLink,
            subscription.UserId,
            ct);
    }

    public virtual async Task ProcessCanonicalDraftOrchestrationAsync(
        Draft canonicalDraft,
        AutomationWorkflow workflow,
        string itemLink,
        Guid userId,
        CancellationToken ct)
    {
        var prompt = !string.IsNullOrWhiteSpace(workflow.InstructionPrompt)
            ? workflow.InstructionPrompt
            : "Summarize this article as a thread.";

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
                userId,
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
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (workflow.Destinations != null && workflow.Destinations.Count > 0)
        {
            foreach (var destination in workflow.Destinations)
            {
                var variant = await _threadGenerationService.GeneratePlatformVariantDraftAsync(
                    canonicalDraft,
                    destination,
                    userId,
                    ct);

                if (variant != null)
                {
                    await _validationService.ValidateDraftAsync(variant, destination.Platform, ct);
                }
            }

            await _autoPublishService.PublishDestinationsAsync(
                canonicalDraft,
                workflow.Destinations,
                userId,
                ct);
        }
    }

    public virtual async Task ContinueWorkflowDraftAfterTranscriptionAsync(
        Guid draftId,
        CancellationToken ct)
    {
        var draft = await _db.Drafts
            .Include(d => d.AutomationWorkflow!)
                .ThenInclude(w => w.Destinations)
            .Include(d => d.DraftSources)
                .ThenInclude(ds => ds.Source)
            .FirstOrDefaultAsync(d => d.Id == draftId && d.Status != DraftStatus.Deleted, ct);

        if (draft == null || draft.AutomationWorkflow == null)
        {
            _logger.LogWarning("Draft {DraftId} not found or has no automation workflow for post-transcription continuation.", draftId);
            return;
        }

        var primarySource = draft.DraftSources.Select(ds => ds.Source).FirstOrDefault();
        var itemLink = primarySource?.Reference ?? string.Empty;

        await ProcessCanonicalDraftOrchestrationAsync(
            draft,
            draft.AutomationWorkflow,
            itemLink,
            draft.UserId,
            ct);
    }
}

