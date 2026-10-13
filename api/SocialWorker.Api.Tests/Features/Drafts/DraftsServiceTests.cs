using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class DraftsServiceTests : SqliteTestBase
{
    private static async Task<(AppDbContext Db, DraftsService Service, AppUser User)> CreateServiceAsync(AppDbContext db)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash"
        };
        db.Users.Add(user);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Test Draft",
            Content = "Initial content",
            UserId = user.Id,
            Status = DraftStatus.Editing
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider();
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);

        return (db, draftsService, user);
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesTitle()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var result = await service.UpdateDraftAsync(user.Id, draft.Id, title: "New Title", null, null, null, null, null, CancellationToken.None);

        Assert.Equal("New Title", result.Title);

        var reloaded = await db.Drafts.FirstAsync(d => d.Id == draft.Id);
        Assert.Equal("New Title", reloaded.Title);
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesContent()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var result = await service.UpdateDraftAsync(user.Id, draft.Id, null, "Updated content", null, null, null, null, CancellationToken.None);

        Assert.Equal("Updated content", result.Content);
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesStatus()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var result = await service.UpdateDraftAsync(user.Id, draft.Id, null, null, "Sourcing", null, null, null, CancellationToken.None);

        Assert.Equal("Sourcing", result.Status);
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesChatHistory()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var chatHistory = """{"messages": [{"role": "user", "content": "hello"}]}""";
        var result = await service.UpdateDraftAsync(user.Id, draft.Id, null, null, null, chatHistory, null, null, CancellationToken.None);

        Assert.Equal(chatHistory, result.ChatHistory);
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesChatSummary()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var result = await service.UpdateDraftAsync(user.Id, draft.Id, null, null, null, null, "test summary", 5, CancellationToken.None);

        Assert.Equal("test summary", result.ChatSummary);
        Assert.Equal(5, result.LastSummarizedMessageCount);
    }

    [Fact]
    public async Task UpdateDraftAsync_Throws_For_Deleted_Draft()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        // Delete it first
        await service.UpdateDraftAsync(user.Id, draft.Id, null, null, "Deleted", null, null, null, CancellationToken.None);

        // Now try to update - should throw
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateDraftAsync(user.Id, draft.Id, null, "New content", null, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateDraftAsync_Throws_For_Wrong_User()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, _) = await CreateServiceAsync(db);

        var wrongUserId = Guid.NewGuid();
        var someDraftId = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateDraftAsync(wrongUserId, someDraftId, null, null, null, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateDraftAsync_ReconcilesSegments()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var multiSegment = "Post one\n\n---\n\nPost two\n\n---\n\nPost three";
        await service.UpdateDraftAsync(user.Id, draft.Id, null, multiSegment, null, null, null, null, CancellationToken.None);

        var segments = await db.ThreadSegments.Where(s => s.DraftId == draft.Id).OrderBy(s => s.Position).ToListAsync();
        Assert.Equal(3, segments.Count);
        Assert.Contains("Post one", segments[0].Content);
        Assert.Contains("Post two", segments[1].Content);
        Assert.Contains("Post three", segments[2].Content);
    }

    [Fact]
    public async Task UpdateDraftAsync_DeletesDraftWithMedia()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var media = new MediaAsset
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            FileName = "test.png",
            FilePath = "test/test.png",
            MimeType = "image/png",
            SizeBytes = 100,
            Width = 100,
            Height = 100
        };
        db.MediaAssets.Add(media);
        await db.SaveChangesAsync();

        await service.UpdateDraftAsync(user.Id, draft.Id, null, null, "Deleted", null, null, null, CancellationToken.None);

        var deleted = await db.Drafts.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == draft.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task UpdateDraftAsync_CompactsChatHistoryInBackground()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var queue = new TestBackgroundTaskQueue();
        var adapter = new SummaryAdapter("Condensed summary of the earlier conversation.");
        var (service, user, draft) = await CreateSummarizationServiceAsync(db, queue, adapter, model: "llama3.1");

        var messages = Enumerable.Range(0, 40)
            .Select(i => new
            {
                role = i % 2 == 0 ? "user" : "assistant",
                content = $"Message {i} {new string('x', 1200)}"
            })
            .ToList();

        var historyJson = JsonSerializer.Serialize(new { messages });

        await service.UpdateDraftAsync(user.Id, draft.Id, null, null, null, historyJson, null, null, CancellationToken.None);

        Assert.Single(queue.EnqueuedTasks);
        var task = queue.EnqueuedTasks[0];
        Assert.Equal("draft-chat-summary", task.TaskType);

        var handler = new SocialWorker.Api.Features.Drafts.Handlers.DraftChatSummaryTaskHandler(
            db,
            adapter,
            new LlmProviderService(),
            NullLogger<SocialWorker.Api.Features.Drafts.Handlers.DraftChatSummaryTaskHandler>.Instance);

        var context = new SocialWorker.Api.Features.BackgroundTasks.Services.TaskExecutionContext(task, "test-worker", queue, CancellationToken.None);
        var result = await handler.ExecuteRawAsync(task.PayloadJson, context, CancellationToken.None);
        Assert.Equal(SocialWorker.Api.Features.BackgroundTasks.Models.TaskOutcome.Success, result.Outcome);

        var reloaded = await db.Drafts.FirstAsync(d => d.Id == draft.Id);
        Assert.Equal("Condensed summary of the earlier conversation.", reloaded.ChatSummary);
        Assert.Equal(0, reloaded.LastSummarizedMessageCount);

        using var compactedDoc = JsonDocument.Parse(reloaded.ChatHistory!);
        var compactedMessages = compactedDoc.RootElement.GetProperty("messages");
        Assert.True(compactedMessages.GetArrayLength() < messages.Count);
    }

    [Fact]
    public async Task SetDraftBlueskyReplyTargetAsync_UpsertsMetadata()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);
        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);

        var updated = await service.SetDraftBlueskyReplyTargetAsync(
            user.Id,
            draft.Id,
            "at://did:plc:root/app.bsky.feed.post/1",
            "root-cid",
            "at://did:plc:parent/app.bsky.feed.post/2",
            "parent-cid",
            "https://bsky.app/profile/example/post/2",
            "author",
            "parent text",
            "https://cdn.bsky.app/avatar.jpg",
            CancellationToken.None);

        Assert.NotNull(updated.BlueskyReplyTarget);
        Assert.Equal("root-cid", updated.BlueskyReplyTarget!.ReplyRootCid);
        Assert.Equal("parent-cid", updated.BlueskyReplyTarget.ReplyParentCid);
        Assert.Equal("https://cdn.bsky.app/avatar.jpg", updated.BlueskyReplyTarget.ReplyParentAvatarUrl);

        var persisted = await db.DraftBlueskyMetadata.SingleAsync(m => m.DraftId == draft.Id);
        Assert.Equal("at://did:plc:root/app.bsky.feed.post/1", persisted.ReplyRootUri);
        Assert.Equal("at://did:plc:parent/app.bsky.feed.post/2", persisted.ReplyParentUri);
    }

    [Fact]
    public async Task SetDraftBlueskyReplyTargetAsync_Throws_WhenAlreadySet()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);
        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);

        await service.SetDraftBlueskyReplyTargetAsync(
            user.Id,
            draft.Id,
            "at://did:plc:root/app.bsky.feed.post/1",
            "root-cid",
            "at://did:plc:parent/app.bsky.feed.post/2",
            "parent-cid",
            null,
            null,
            null,
            null,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetDraftBlueskyReplyTargetAsync(
                user.Id,
                draft.Id,
                "at://did:plc:root/app.bsky.feed.post/3",
                "root-cid-2",
                "at://did:plc:parent/app.bsky.feed.post/4",
                "parent-cid-2",
                "https://bsky.app/profile/example/post/4",
                "author-2",
                "parent text 2",
                null,
                CancellationToken.None));

        var persisted = await db.DraftBlueskyMetadata.SingleAsync(m => m.DraftId == draft.Id);
        Assert.Equal("root-cid", persisted.ReplyRootCid);
        Assert.Equal("parent-cid", persisted.ReplyParentCid);
    }

    [Fact]
    public async Task SetDraftBlueskyReplyTargetFromUrlAsync_SetsMetadataFromResolverResult()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);
        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);

        var resolver = new FakeBlueskyReplyTargetResolver(new BlueskyReplyTargetResolutionResult(
            true,
            null,
            "at://did:plc:root/app.bsky.feed.post/1",
            "root-cid",
            "at://did:plc:parent/app.bsky.feed.post/2",
            "parent-cid",
            "https://bsky.app/profile/example/post/2",
            "example",
            "hello",
            "https://cdn.bsky.app/avatar.jpg"));

        var updated = await service.SetDraftBlueskyReplyTargetFromUrlAsync(
            user.Id,
            draft.Id,
            "https://bsky.app/profile/example/post/2",
            resolver,
            CancellationToken.None);

        Assert.NotNull(updated.BlueskyReplyTarget);
        Assert.Equal("root-cid", updated.BlueskyReplyTarget!.ReplyRootCid);
        Assert.Equal("parent-cid", updated.BlueskyReplyTarget.ReplyParentCid);
        Assert.Equal("https://bsky.app/profile/example/post/2", updated.BlueskyReplyTarget.ReplyParentUrl);
    }

    [Fact]
    public async Task SetDraftBlueskyReplyTargetFromUrlAsync_Throws_WhenResolverFails()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);
        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);

        var resolver = new FakeBlueskyReplyTargetResolver(new BlueskyReplyTargetResolutionResult(false, "bad url"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetDraftBlueskyReplyTargetFromUrlAsync(
                user.Id,
                draft.Id,
                "https://bsky.app/profile/example/post/2",
                resolver,
                CancellationToken.None));
    }

    [Fact]
    public async Task SetDraftBlueskyReplyTargetAsync_Throws_WhenDraftHasSentThread()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);
        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);

        db.PlatformThreads.Add(new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Sent,
            Content = draft.Content,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetDraftBlueskyReplyTargetAsync(
                user.Id,
                draft.Id,
                "at://did:plc:root/app.bsky.feed.post/1",
                "root-cid",
                "at://did:plc:parent/app.bsky.feed.post/2",
                "parent-cid",
                null,
                null,
                null,
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateReplyDraftFromBlueskyPostUrlAsync_CreatesNewDraftWithReplyTarget()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);
        var beforeCount = await db.Drafts.CountAsync(d => d.UserId == user.Id);

        var resolver = new FakeBlueskyReplyTargetResolver(new BlueskyReplyTargetResolutionResult(
            true,
            null,
            "at://did:plc:root/app.bsky.feed.post/1",
            "root-cid",
            "at://did:plc:parent/app.bsky.feed.post/2",
            "parent-cid",
            "https://bsky.app/profile/example/post/2",
            "example",
            "hello",
            "https://cdn.bsky.app/avatar.jpg"));

        var created = await service.CreateReplyDraftFromBlueskyPostUrlAsync(
            user.Id,
            "https://bsky.app/profile/example/post/2",
            "Reply draft",
            "",
            resolver,
            CancellationToken.None);

        var afterCount = await db.Drafts.CountAsync(d => d.UserId == user.Id);
        Assert.Equal(beforeCount + 1, afterCount);
        Assert.Equal("Reply draft", created.Title);
        Assert.NotNull(created.BlueskyReplyTarget);
        Assert.Equal("root-cid", created.BlueskyReplyTarget!.ReplyRootCid);
    }

    private static async Task<(DraftsService Service, AppUser User, Draft Draft)> CreateSummarizationServiceAsync(AppDbContext db, IBackgroundTaskQueue queue, ILlmProviderAdapter adapter, string model = "gpt-4o-mini")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "summaryuser",
            Email = "summary@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
        db.Users.Add(user);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "default",
            ProviderType = "OpenAI",
            BaseUrl = "https://test.local/v1",
            ApiKey = "test-key",
            Model = model,
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Summary Draft",
            Content = "Initial content",
            UserId = user.Id,
            Status = DraftStatus.Editing
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider();
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService, queue: queue);
        return (draftsService, user, draft);
    }

    [Fact]
    public async Task DeleteDraftAsync_DeletesDraftAndAssociatedQueueItems()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = await db.Drafts.FirstAsync(d => d.UserId == user.Id);
        var schedule = Schedule.Create(user.Id, "Test Schedule");
        db.Schedules.Add(schedule);

        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "MetaThreads", Stage = PlatformThreadStage.Scheduled };
        db.PlatformThreads.Add(thread);

        var queueItem = ScheduleQueueItem.Create(schedule.Id, draft.Id, thread.Id, 0);
        db.ScheduleQueueItems.Add(queueItem);
        await db.SaveChangesAsync();

        var deleted = await service.DeleteDraftAsync(user.Id, draft.Id);
        Assert.True(deleted);

        var draftInDb = await db.Drafts.FindAsync(draft.Id);
        Assert.Null(draftInDb);

        var queueItemInDb = await db.ScheduleQueueItems.FindAsync(queueItem.Id);
        Assert.Null(queueItemInDb);
    }

    [Fact]
    public async Task PurgeWorkflowDraftsAsync_PurgesOnlyUnpublishedDraftsForMatchingWorkflow()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var wf1 = new AutomationWorkflow { Id = Guid.NewGuid(), UserId = user.Id, Title = "Target WF", InstructionPrompt = "P" };
        var wf2 = new AutomationWorkflow { Id = Guid.NewGuid(), UserId = user.Id, Title = "Other WF", InstructionPrompt = "P" };
        db.AutomationWorkflows.AddRange(wf1, wf2);

        var targetWorkflowId = wf1.Id;
        var otherWorkflowId = wf2.Id;

        var unpublishedTarget = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Unpub Target", AutomationWorkflowId = targetWorkflowId };
        var publishedTarget = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Pub Target", AutomationWorkflowId = targetWorkflowId };
        var otherDraft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Other", AutomationWorkflowId = otherWorkflowId };

        db.Drafts.AddRange(unpublishedTarget, publishedTarget, otherDraft);

        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = publishedTarget.Id, Platform = "MetaThreads", Stage = PlatformThreadStage.Sent };
        db.PlatformThreads.Add(thread);

        var post = new Post { Id = Guid.NewGuid(), DraftId = publishedTarget.Id, PlatformThreadId = thread.Id, Platform = "MetaThreads", RemoteId = "remote-1" };
        db.Posts.Add(post);

        await db.SaveChangesAsync();

        var purgedCount = await service.PurgeWorkflowDraftsAsync(user.Id, targetWorkflowId, unpublishedOnly: true);

        Assert.Equal(1, purgedCount);

        Assert.Null(await db.Drafts.FindAsync(unpublishedTarget.Id));
        Assert.NotNull(await db.Drafts.FindAsync(publishedTarget.Id));
        Assert.NotNull(await db.Drafts.FindAsync(otherDraft.Id));
    }

    [Fact]
    public async Task DiscardDraftAsync_SetsStatusAndCancelsScheduledQueueItems()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Draft to Discard",
            Status = DraftStatus.Editing
        };
        db.Drafts.Add(draft);

        var schedule = Schedule.Create(user.Id, "Test Schedule", "America/Sao_Paulo");
        db.Schedules.Add(schedule);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Stage = PlatformThreadStage.Scheduled
        };
        db.PlatformThreads.Add(thread);

        var queueItem = ScheduleQueueItem.Create(schedule.Id, draft.Id, thread.Id, 1);
        queueItem.Status = ScheduleQueueItemStatus.Queued;
        db.ScheduleQueueItems.Add(queueItem);

        await db.SaveChangesAsync();

        var success = await service.DiscardDraftAsync(user.Id, draft.Id, "Irrelevant news topic");

        Assert.True(success);

        var reloadedDraft = await db.Drafts.FindAsync(draft.Id);
        Assert.NotNull(reloadedDraft);
        Assert.Equal(DraftStatus.Discarded, reloadedDraft.Status);
        Assert.Equal("Irrelevant news topic", reloadedDraft.DiscardReason);

        var reloadedQueue = await db.ScheduleQueueItems.FindAsync(queueItem.Id);
        Assert.Null(reloadedQueue);
    }

    [Fact]
    public async Task GetDraftsForUserAsync_ExcludesDiscarded_UnlessExplicitlyRequested()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, service, user) = await CreateServiceAsync(db);

        var normalDraft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Normal", Status = DraftStatus.Editing };
        var discardedDraft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Discarded", Status = DraftStatus.Discarded, DiscardReason = "Low quality" };
        db.Drafts.AddRange(normalDraft, discardedDraft);
        await db.SaveChangesAsync();

        var defaultList = await service.GetDraftsForUserAsync(user.Id);
        Assert.Contains(defaultList, d => d.Id == normalDraft.Id);
        Assert.DoesNotContain(defaultList, d => d.Id == discardedDraft.Id);

        var filteredList = await service.GetDraftsForUserAsync(user.Id, new DraftQueryFilter(Status: DraftStatus.Discarded));
        Assert.Contains(filteredList, d => d.Id == discardedDraft.Id);
        Assert.DoesNotContain(filteredList, d => d.Id == normalDraft.Id);
    }
}

/// <summary>
/// Creates scopes that provide the same DbContext instance (for testing).
/// </summary>
public sealed class MockScopeFactory : IServiceScopeFactory
{
    private readonly AppDbContext _db;

    public MockScopeFactory(AppDbContext db) => _db = db;

    public IServiceScope CreateScope() => new MockScope(_db);

    private sealed class MockScope : IServiceScope
    {
        private readonly AppDbContext _db;
        public IServiceProvider ServiceProvider { get; }

        public MockScope(AppDbContext db)
        {
            _db = db;
            ServiceProvider = new MockServiceProvider(db);
        }

        public void Dispose() { }
    }

    private sealed class MockServiceProvider : IServiceProvider
    {
        private readonly AppDbContext _db;
        public MockServiceProvider(AppDbContext db) => _db = db;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(AppDbContext)) return _db;
            if (serviceType == typeof(WebScraperService)) return null;
            return null;
        }
    }
}

public sealed class SummaryScopeFactory : IServiceScopeFactory
{
    private readonly IServiceProvider _provider;

    public SummaryScopeFactory(AppDbContext db, ILlmProviderAdapter adapter)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(adapter);
        services.AddSingleton<ILlmProviderAdapter>(adapter);
        services.AddSingleton<ILogger<DraftsService>>(NullLogger<DraftsService>.Instance);
        services.AddSingleton<LlmProviderService>();
        _provider = services.BuildServiceProvider();
    }

    public IServiceScope CreateScope() => new Scope(_provider);

    private sealed class Scope : IServiceScope
    {
        public Scope(IServiceProvider provider) => ServiceProvider = provider;
        public IServiceProvider ServiceProvider { get; }
        public void Dispose() { }
    }
}

public sealed class SummaryAdapter : ILlmProviderAdapter
{
    private readonly string _summary;

    public SummaryAdapter(string summary)
    {
        _summary = summary;
    }

    public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return new OpenAiModels.StreamChunk
        {
            Choices = new() { new OpenAiModels.StreamChoice { Delta = new OpenAiModels.StreamDelta(), FinishReason = "stop" } }
        };
    }

    public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
    {
        return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new()
            {
                new OpenAiModels.ChatCompletionChoice
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = _summary
                    }
                }
            }
        });
    }
}

internal sealed class FakeBlueskyReplyTargetResolver : IBlueskyReplyTargetResolver
{
    private readonly BlueskyReplyTargetResolutionResult _result;

    public FakeBlueskyReplyTargetResolver(BlueskyReplyTargetResolutionResult result)
    {
        _result = result;
    }

    public Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct)
    {
        return Task.FromResult(_result);
    }

    public Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct)
    {
        return Task.FromResult<string?>(null);
    }
}