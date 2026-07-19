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
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Background;
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
        var queue = new BackgroundJobQueue();
        var scopeFactory = new MockScopeFactory(db);
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService, scopeFactory, queue);

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

        var queue = new BackgroundJobQueue();
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

        var job = await queue.ReadAsync(CancellationToken.None);
        await job.Work(CancellationToken.None);

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

    private static async Task<(DraftsService Service, AppUser User, Draft Draft)> CreateSummarizationServiceAsync(AppDbContext db, BackgroundJobQueue queue, ILlmProviderAdapter adapter, string model = "gpt-4o-mini")
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
        var scopeFactory = new SummaryScopeFactory(db, adapter);
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService, scopeFactory, queue);
        return (draftsService, user, draft);
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