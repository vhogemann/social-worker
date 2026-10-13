using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedOrchestrationServiceTests : SqliteTestBase
{
    private sealed class FakeSourceIngestionService : FeedSourceIngestionService
    {
        public FeedSourceIngestionResult ResultToReturn { get; set; }

        public FakeSourceIngestionService(AppDbContext db, FeedSourceIngestionResult result)
            : base(db, TestServiceFactory.CreateSourcesService(db), NullLogger<FeedSourceIngestionService>.Instance)
        {
            ResultToReturn = result;
        }

        public override Task<FeedSourceIngestionResult> IngestSourceAsync(
            FeedSubscription subscription,
            AutomationWorkflow workflow,
            string itemTitle,
            string itemLink,
            string itemDescription,
            CancellationToken ct)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeThreadGenerationService : FeedThreadGenerationService
    {
        public int GenerationAttempts { get; private set; }
        public string? LastPromptPassed { get; private set; }
        public bool SucceedsOnAttempt { get; set; } = true;
        public int FailUntilAttempt { get; set; } = 0;
        public List<string> GeneratedPlatforms { get; } = new();

        public FakeThreadGenerationService(AppDbContext db)
            : base(db, null!, NullLogger<FeedThreadGenerationService>.Instance)
        {
        }

        public override Task<bool> GenerateCanonicalThreadAsync(
            Draft draft,
            string itemLink,
            string instructionPrompt,
            Guid userId,
            CancellationToken ct)
        {
            GenerationAttempts++;
            LastPromptPassed = instructionPrompt;

            if (GenerationAttempts <= FailUntilAttempt)
            {
                return Task.FromResult(false);
            }

            draft.Content = "Generated thread content";
            return Task.FromResult(SucceedsOnAttempt);
        }

        public override Task<Draft?> GeneratePlatformVariantDraftAsync(
            Draft canonicalDraft,
            AutomationWorkflowDestination destination,
            Guid userId,
            CancellationToken ct)
        {
            GeneratedPlatforms.Add(destination.Platform);
            var variant = Draft.Create(userId, $"{canonicalDraft.Title} ({destination.Platform})", canonicalDraft.Content);
            return Task.FromResult<Draft?>(variant);
        }
    }

    private sealed class FakeValidationService : DraftValidationService
    {
        public int ValidationAttempts { get; private set; }
        public bool AlwaysValid { get; set; } = true;
        public int FailUntilAttempt { get; set; } = 0;
        public string? RejectionFeedback { get; set; } = "Content rejected";

        public FakeValidationService(AppDbContext db)
            : base(db, null!, NullLogger<DraftValidationService>.Instance)
        {
        }

        public override Task<ValidationResult> ValidateDraftAsync(
            Draft draft,
            string? platformConstraint,
            CancellationToken ct)
        {
            ValidationAttempts++;

            if (ValidationAttempts <= FailUntilAttempt)
            {
                return Task.FromResult(new ValidationResult(false, RejectionFeedback));
            }

            return Task.FromResult(AlwaysValid
                ? new ValidationResult(true, null)
                : new ValidationResult(false, RejectionFeedback));
        }
    }

    private sealed class FakeAutoPublishService : FeedAutoPublishService
    {
        public bool PublishCalled { get; private set; }
        public Draft? PublishedDraft { get; private set; }
        public ICollection<AutomationWorkflowDestination>? PublishedDestinations { get; private set; }

        public FakeAutoPublishService(AppDbContext db)
            : base(db, null!, NullLogger<FeedAutoPublishService>.Instance)
        {
        }

        public override Task PublishDestinationsAsync(
            Draft canonicalDraft,
            ICollection<AutomationWorkflowDestination> destinations,
            Guid userId,
            CancellationToken ct)
        {
            PublishCalled = true;
            PublishedDraft = canonicalDraft;
            PublishedDestinations = destinations;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(IngestionStatus.Duplicate)]
    [InlineData(IngestionStatus.FilteredOut)]
    [InlineData(IngestionStatus.Failed)]
    [InlineData(IngestionStatus.PendingTranscription)]
    public async Task ProcessFeedItemAsync_WhenIngestionStatusIsNotSuccess_AbortsOrchestration(IngestionStatus status)
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");

        var ingestionResult = new FeedSourceIngestionResult(status, null, null, "Skipped");
        var ingestionService = new FakeSourceIngestionService(db, ingestionResult);
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db);
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal(0, generationService.GenerationAttempts);
        Assert.False(publishService.PublishCalled);
    }

    [Fact]
    public async Task ProcessFeedItemAsync_HappyPath_GeneratesCanonical_CreatesVariants_AndPublishes()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "Tech Workflow", "Draft a witty thread.");
        var dest1 = AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", true);
        var dest2 = AutomationWorkflowDestination.Create(workflow.Id, "LinkedIn", false);
        workflow.Destinations.Add(dest1);
        workflow.Destinations.Add(dest2);

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft", "Initial content");
        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, canonicalDraft, Guid.NewGuid(), null));
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db);
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal(1, generationService.GenerationAttempts);
        Assert.Equal("Draft a witty thread.", generationService.LastPromptPassed);
        Assert.Contains("Bluesky", generationService.GeneratedPlatforms);
        Assert.Contains("LinkedIn", generationService.GeneratedPlatforms);
        Assert.True(publishService.PublishCalled);
        Assert.Equal(canonicalDraft, publishService.PublishedDraft);
    }

    [Fact]
    public async Task ContinueWorkflowDraftAfterTranscriptionAsync_GeneratesCanonical_CreatesVariants_AndPublishes()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "Video WF", "Analyze this video.");
        var dest = AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", true);
        workflow.Destinations.Add(dest);
        db.AutomationWorkflows.Add(workflow);

        var draft = Draft.Create(user.Id, "Transcribed Draft", "");
        draft.AutomationWorkflowId = workflow.Id;
        draft.Status = DraftStatus.Sourcing;
        db.Drafts.Add(draft);

        var source = Source.CreateUrl("https://youtube.com/watch?v=sample123", "Video Title");
        source.Kind = SourceKind.YouTube;
        source.ProcessingStatus = SourceProcessingStatus.Complete;
        source.Content = "Transcribed speech content";
        db.Sources.Add(source);

        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, draft, source.Id, null));
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db);
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ContinueWorkflowDraftAfterTranscriptionAsync(draft.Id, CancellationToken.None);

        Assert.Equal(1, generationService.GenerationAttempts);
        Assert.Equal("Analyze this video.", generationService.LastPromptPassed);
        Assert.Contains("Bluesky", generationService.GeneratedPlatforms);
        Assert.True(publishService.PublishCalled);
    }

    [Fact]
    public async Task ProcessFeedItemAsync_SelfCorrectionLoop_RetriesWhenGenerationFails_AndSucceedsOnSecondAttempt()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft");
        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, canonicalDraft, Guid.NewGuid(), null));
        var generationService = new FakeThreadGenerationService(db) { FailUntilAttempt = 1 };
        var validationService = new FakeValidationService(db);
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal(2, generationService.GenerationAttempts);
        Assert.NotEqual(DraftStatus.Failed, canonicalDraft.Status);
    }

    [Fact]
    public async Task ProcessFeedItemAsync_SelfCorrectionLoop_RetriesWhenValidationFails_AndSucceedsOnSecondAttempt()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft");
        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, canonicalDraft, Guid.NewGuid(), null));
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db) { FailUntilAttempt = 1, RejectionFeedback = "Exceeded 500 characters" };
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal(2, generationService.GenerationAttempts);
        Assert.Equal(2, validationService.ValidationAttempts);
        Assert.NotEqual(DraftStatus.Failed, canonicalDraft.Status);
        Assert.Contains("Exceeded 500 characters", generationService.LastPromptPassed);
    }

    [Fact]
    public async Task ProcessFeedItemAsync_SelfCorrectionLoop_ExhaustsMaxAttempts_MarksCanonicalDraftFailed()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", true));

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft");
        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, canonicalDraft, Guid.NewGuid(), null));
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db) { AlwaysValid = false };
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal(3, generationService.GenerationAttempts);
        Assert.Equal(3, validationService.ValidationAttempts);
        Assert.Equal(DraftStatus.Failed, canonicalDraft.Status);
        Assert.Empty(generationService.GeneratedPlatforms);
        Assert.False(publishService.PublishCalled);
    }

    [Fact]
    public async Task ProcessFeedItemAsync_FallsBackToDefaultPrompt_WhenWorkflowPromptIsEmpty()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = new AutomationWorkflow { Id = Guid.NewGuid(), UserId = user.Id, Title = "No Prompt WF", InstructionPrompt = "" };

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft");
        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, canonicalDraft, Guid.NewGuid(), null));
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db);
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal("Summarize this article as a thread.", generationService.LastPromptPassed);
    }

    [Fact]
    public async Task ProcessFeedItemAsync_WhenWorkflowHasZeroDestinations_SucceedsWithoutVariantsOrPublishing()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "Zero Dest WF", "Summarize");

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft");
        var ingestionService = new FakeSourceIngestionService(db, new FeedSourceIngestionResult(IngestionStatus.Success, canonicalDraft, Guid.NewGuid(), null));
        var generationService = new FakeThreadGenerationService(db);
        var validationService = new FakeValidationService(db);
        var publishService = new FakeAutoPublishService(db);

        var orchestrator = new FeedOrchestrationService(
            db,
            ingestionService,
            generationService,
            validationService,
            publishService,
            NullLogger<FeedOrchestrationService>.Instance);

        await orchestrator.ProcessFeedItemAsync(sub, workflow, "Title", "https://example.com/item", "Desc", null, CancellationToken.None);

        Assert.Equal(1, generationService.GenerationAttempts);
        Assert.Empty(generationService.GeneratedPlatforms);
        Assert.False(publishService.PublishCalled);
    }
}

