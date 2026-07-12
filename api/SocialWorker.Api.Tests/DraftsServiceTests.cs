using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Background;
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

        var sourcesService = new SourcesService(db, null!, null!, null!);
        var storage = new FileStorageProvider();
        var queue = new BackgroundJobQueue();
        var scopeFactory = new MockScopeFactory(db);
        var draftsService = new DraftsService(db, storage, sourcesService, scopeFactory, queue);

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