using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Workflows.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class DigestWorkflowSourceHandlerTests : SqliteTestBase
{
    private sealed class FakeFeedOrchestrationService : FeedOrchestrationService
    {
        public bool WasOrchestrationCalled { get; private set; }
        public Draft? OrchestratedDraft { get; private set; }
        public AutomationWorkflow? OrchestratedWorkflow { get; private set; }

        public FakeFeedOrchestrationService(AppDbContext db)
            : base(db, null!, null!, null!, null!, NullLogger<FeedOrchestrationService>.Instance)
        {
        }

        public override Task ProcessCanonicalDraftOrchestrationAsync(
            Draft canonicalDraft,
            AutomationWorkflow workflow,
            string itemLink,
            Guid userId,
            CancellationToken ct)
        {
            WasOrchestrationCalled = true;
            OrchestratedDraft = canonicalDraft;
            OrchestratedWorkflow = workflow;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenNoPublishedDrafts_ReturnsZeroAndSkipsGeneration()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);

        var sourceWf = AutomationWorkflow.Create(user.Id, "Source Workflow", "Instruction");
        var digestWf = AutomationWorkflow.Create(user.Id, "Weekly Recap", "Create a recap");
        db.AutomationWorkflows.AddRange(sourceWf, digestWf);

        var source = AutomationWorkflowSource.CreateWorkflowHighlightsSource(digestWf.Id, sourceWf.Id);
        db.AutomationWorkflowSources.Add(source);
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider();
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);
        var fakeOrchestration = new FakeFeedOrchestrationService(db);

        var handler = new DigestWorkflowSourceHandler(
            db,
            draftsService,
            fakeOrchestration,
            NullLogger<DigestWorkflowSourceHandler>.Instance
        );

        var result = await handler.ProcessAsync(digestWf, source, CancellationToken.None);

        Assert.Equal(0, result);
        Assert.False(fakeOrchestration.WasOrchestrationCalled);
    }

    [Fact]
    public async Task ProcessAsync_WhenPublishedDraftsExist_CreatesDigestDraftAndOrchestrates()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);

        var sourceWf = AutomationWorkflow.Create(user.Id, "Source Workflow", "Instruction");
        var digestWf = AutomationWorkflow.Create(user.Id, "Weekly Highlights", "Summarize weekly highlights with links.");
        db.AutomationWorkflows.AddRange(sourceWf, digestWf);

        var draft = Draft.Create(user.Id, "Great Tech News", "This is an insightful tech post.", SocialPlatform.Bluesky, automationWorkflowId: sourceWf.Id);
        db.Drafts.Add(draft);

        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Sent, Content = draft.Content };
        db.PlatformThreads.Add(thread);

        var post = new Post { Id = Guid.NewGuid(), DraftId = draft.Id, PlatformThreadId = thread.Id, SegmentIndex = 0, Platform = "Bluesky", Url = "https://bsky.app/profile/tech/post/123" };
        db.Posts.Add(post);

        var source = AutomationWorkflowSource.CreateWorkflowHighlightsSource(digestWf.Id, sourceWf.Id, "{\"lookbackDays\": 7}");
        db.AutomationWorkflowSources.Add(source);
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider();
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);
        var fakeOrchestration = new FakeFeedOrchestrationService(db);

        var handler = new DigestWorkflowSourceHandler(
            db,
            draftsService,
            fakeOrchestration,
            NullLogger<DigestWorkflowSourceHandler>.Instance
        );

        var result = await handler.ProcessAsync(digestWf, source, CancellationToken.None);

        Assert.Equal(1, result);
        Assert.True(fakeOrchestration.WasOrchestrationCalled);
        Assert.NotNull(fakeOrchestration.OrchestratedDraft);
        Assert.Equal(digestWf.Id, fakeOrchestration.OrchestratedDraft.AutomationWorkflowId);

        // Verify digest source was attached to the draft
        var attachedSources = await db.DraftSources
            .Include(ds => ds.Source)
            .Where(ds => ds.DraftId == fakeOrchestration.OrchestratedDraft.Id)
            .ToListAsync();

        Assert.Single(attachedSources);
        Assert.Contains("Great Tech News", attachedSources[0].Source.Content);
        Assert.Contains("https://bsky.app/profile/tech/post/123", attachedSources[0].Source.Content);
    }
}
