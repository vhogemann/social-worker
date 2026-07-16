using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Background;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class SetBlueskyReplyTargetToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_SetsReplyTarget_WhenResolverSucceeds()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing });
        await db.SaveChangesAsync();

        var services = BuildServices(db, new StubResolver(_ => new BlueskyReplyTargetResolutionResult(
            true,
            null,
            "at://did:plc:root/app.bsky.feed.post/1",
            "root-cid",
            "at://did:plc:parent/app.bsky.feed.post/2",
            "parent-cid",
            "https://bsky.app/profile/example/post/2",
            "example.bsky.social",
            "Parent text",
            "https://cdn.bsky.app/avatar.jpg"
        )));

        var tool = new SetBlueskyReplyTargetTool(services.GetRequiredService<IServiceScopeFactory>());
        var result = await tool.ExecuteAsync(new SetBlueskyReplyTargetArgs("https://bsky.app/profile/example/post/2"), draftId, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("https://bsky.app/profile/example/post/2", result.ReplyParentUrl);

        var persisted = await db.DraftBlueskyMetadata.FindAsync(draftId);
        Assert.NotNull(persisted);
        Assert.Equal("parent-cid", persisted!.ReplyParentCid);
        Assert.Equal("https://cdn.bsky.app/avatar.jpg", persisted.ReplyParentAvatarUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenReplyTargetAlreadySet()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing });
        db.DraftBlueskyMetadata.Add(new DraftBlueskyMetadata
        {
            DraftId = draftId,
            ReplyRootUri = "at://did:plc:root/app.bsky.feed.post/1",
            ReplyRootCid = "root-cid",
            ReplyParentUri = "at://did:plc:parent/app.bsky.feed.post/2",
            ReplyParentCid = "parent-cid"
        });
        await db.SaveChangesAsync();

        var services = BuildServices(db, new StubResolver(_ => new BlueskyReplyTargetResolutionResult(
            true,
            null,
            "at://did:plc:root/app.bsky.feed.post/1",
            "root-cid",
            "at://did:plc:parent/app.bsky.feed.post/2",
            "parent-cid",
            "https://bsky.app/profile/example/post/2",
            "example.bsky.social",
            "Parent text",
            "https://cdn.bsky.app/avatar.jpg"
        )));

        var tool = new SetBlueskyReplyTargetTool(services.GetRequiredService<IServiceScopeFactory>());
        var result = await tool.ExecuteAsync(new SetBlueskyReplyTargetArgs("https://bsky.app/profile/example/post/2"), draftId, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("already set", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenResolverFails()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing });
        await db.SaveChangesAsync();

        var services = BuildServices(db, new StubResolver(_ => new BlueskyReplyTargetResolutionResult(false, "Only strict bsky.app post URLs are supported.")));

        var tool = new SetBlueskyReplyTargetTool(services.GetRequiredService<IServiceScopeFactory>());
        var result = await tool.ExecuteAsync(new SetBlueskyReplyTargetArgs("https://example.com/not-allowed"), draftId, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("strict", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.DraftBlueskyMetadata.AnyAsync(m => m.DraftId == draftId));
    }

    private static ServiceProvider BuildServices(AppDbContext db, IBlueskyReplyTargetResolver resolver)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(resolver);
        services.AddSingleton(new SourcesService(db, null!, null!, null!));
        services.AddSingleton<FileStorageProvider>();
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<DraftsService>();
        return services.BuildServiceProvider();
    }

    private sealed class StubResolver : IBlueskyReplyTargetResolver
    {
        private readonly Func<string, BlueskyReplyTargetResolutionResult> _resolve;

        public StubResolver(Func<string, BlueskyReplyTargetResolutionResult> resolve)
        {
            _resolve = resolve;
        }

        public Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct)
        {
            return Task.FromResult(_resolve(url));
        }

        public Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
