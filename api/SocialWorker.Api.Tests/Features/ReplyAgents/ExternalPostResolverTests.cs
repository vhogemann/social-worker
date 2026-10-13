using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.ReplyAgents.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public class ExternalPostResolverTests : SqliteTestBase
{
    private sealed class FakeBlueskyResolver : IBlueskyReplyTargetResolver
    {
        private readonly Func<string, BlueskyReplyTargetResolutionResult> _handler;

        public FakeBlueskyResolver(Func<string, BlueskyReplyTargetResolutionResult> handler)
        {
            _handler = handler;
        }

        public Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct = default)
        {
            return Task.FromResult(_handler(url));
        }

        public Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task ResolveOrCreatePostAsync_WithValidBlueskyUrl_CreatesAndPersistsExternalPost()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeBlueskyResolver(url => new BlueskyReplyTargetResolutionResult(
            Success: true,
            Error: null,
            ReplyRootUri: "at://did:plc:alice123/app.bsky.feed.post/3la7xyz",
            ReplyRootCid: "bafyroot",
            ReplyParentUri: "at://did:plc:alice123/app.bsky.feed.post/3la7xyz",
            ReplyParentCid: "bafyparent",
            ReplyParentUrl: "https://bsky.app/profile/alice.bsky.social/post/3la7xyz",
            ReplyParentAuthor: "alice.bsky.social",
            ReplyParentText: "Viral post about AI agents",
            ReplyParentAvatarUrl: "https://cdn.bsky.app/avatar.jpg"));

        var resolver = new ExternalPostResolver(db, fakeResolver, NullLogger<ExternalPostResolver>.Instance);

        var result = await resolver.ResolveOrCreatePostAsync(user.Id, "https://bsky.app/profile/alice.bsky.social/post/3la7xyz");

        Assert.True(result.Success);
        Assert.NotNull(result.Post);
        Assert.Equal("Bluesky", result.Post.Platform);
        Assert.Equal("at://did:plc:alice123/app.bsky.feed.post/3la7xyz", result.Post.RemoteId);
        Assert.Equal("bafyparent", result.Post.RemoteCid);
        Assert.Equal("alice.bsky.social", result.Post.AuthorHandle);
        Assert.Equal("Viral post about AI agents", result.Post.PublishedText);
        Assert.True(result.Post.IsExternal);
        Assert.Null(result.Post.DraftId);

        var persisted = await db.Posts.FirstOrDefaultAsync(p => p.Id == result.Post.Id);
        Assert.NotNull(persisted);
        Assert.Equal(user.Id, persisted.UserId);
    }

    [Fact]
    public async Task ResolveOrCreatePostAsync_WhenPostAlreadyExists_ReturnsExistingPostWithoutDuplication()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var existingPost = Post.CreateExternal(
            userId: user.Id,
            platform: "Bluesky",
            remoteId: "at://did:plc:alice123/app.bsky.feed.post/3la7xyz",
            remoteCid: "bafyparent",
            publishedText: "Original text",
            url: "https://bsky.app/profile/alice.bsky.social/post/3la7xyz",
            authorHandle: "alice.bsky.social");
        db.Posts.Add(existingPost);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeBlueskyResolver(_ => new BlueskyReplyTargetResolutionResult(false, "Should not be called"));
        var resolver = new ExternalPostResolver(db, fakeResolver, NullLogger<ExternalPostResolver>.Instance);

        var result = await resolver.ResolveOrCreatePostAsync(user.Id, "https://bsky.app/profile/alice.bsky.social/post/3la7xyz");

        Assert.True(result.Success);
        Assert.NotNull(result.Post);
        Assert.Equal(existingPost.Id, result.Post.Id);

        var postCount = await db.Posts.CountAsync();
        Assert.Equal(1, postCount);
    }

    [Fact]
    public async Task ResolveOrCreatePostAsync_WithInvalidUrl_ReturnsError()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeBlueskyResolver(_ => new BlueskyReplyTargetResolutionResult(false, "Invalid URL"));

        var resolver = new ExternalPostResolver(db, fakeResolver, NullLogger<ExternalPostResolver>.Instance);

        var result = await resolver.ResolveOrCreatePostAsync(user.Id, "https://invalid.url/unknown");

        Assert.False(result.Success);
        Assert.Null(result.Post);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ResolveOrCreatePostAsync_WithThreadsUrl_ResolvesShortcodeToNumericMediaId()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var key = Convert.ToBase64String(new byte[32]);
        var account = new Account
        {
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "vhogemann",
            ExternalId = "12345",
            CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString("token_abc", key)
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:DbEncryptionKey"] = key })
            .Build();

        var fakeApiClient = new FakeMetaThreadsApiClient(_ => Task.FromResult(new SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsUserThreadsResponse(
            new List<SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsUserThreadItem>
            {
                new("18131617717690195", "TEXT_POST", "Post sobre divida publica", "https://www.threads.net/@vhogemann/post/18131617717690195", "DeYvHQHiA4S")
            }
        )));

        var fakeResolver = new FakeBlueskyResolver(_ => new BlueskyReplyTargetResolutionResult(false, "Not used"));
        var resolver = new ExternalPostResolver(db, fakeResolver, NullLogger<ExternalPostResolver>.Instance, fakeApiClient, config);

        var result = await resolver.ResolveOrCreatePostAsync(user.Id, "https://www.threads.com/@vhogemann/post/DeYvHQHiA4S");

        Assert.True(result.Success);
        Assert.NotNull(result.Post);
        Assert.Equal("MetaThreads", result.Post.Platform);
        Assert.Equal("18131617717690195", result.Post.RemoteId);
        Assert.Equal("Post sobre divida publica", result.Post.PublishedText);
        Assert.Equal("https://www.threads.net/@vhogemann/post/18131617717690195", result.Post.Url);
        Assert.True(result.Post.IsExternal);
    }

    [Fact]
    public async Task ResolveOrCreatePostAsync_WithValidTwitterUrl_CreatesAndPersistsExternalPost()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeBlueskyResolver(_ => new BlueskyReplyTargetResolutionResult(false, "Not used"));
        var resolver = new ExternalPostResolver(db, fakeResolver, NullLogger<ExternalPostResolver>.Instance);

        var result = await resolver.ResolveOrCreatePostAsync(user.Id, "https://twitter.com/jack/status/20");

        Assert.True(result.Success);
        Assert.NotNull(result.Post);
        Assert.Equal("Twitter", result.Post.Platform);
        Assert.Equal("20", result.Post.RemoteId);
        Assert.Equal("jack", result.Post.AuthorHandle);
        Assert.Equal("https://twitter.com/jack/status/20", result.Post.Url);
        Assert.True(result.Post.IsExternal);

        var persisted = await db.Posts.FirstOrDefaultAsync(p => p.Id == result.Post.Id);
        Assert.NotNull(persisted);
        Assert.Equal(user.Id, persisted.UserId);
    }

    [Fact]
    public async Task ResolveOrCreatePostAsync_WithValidXUrl_CreatesAndPersistsExternalPost()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeBlueskyResolver(_ => new BlueskyReplyTargetResolutionResult(false, "Not used"));
        var resolver = new ExternalPostResolver(db, fakeResolver, NullLogger<ExternalPostResolver>.Instance);

        var result = await resolver.ResolveOrCreatePostAsync(user.Id, "https://x.com/vhogemann/status/1234567890123456789");

        Assert.True(result.Success);
        Assert.NotNull(result.Post);
        Assert.Equal("Twitter", result.Post.Platform);
        Assert.Equal("1234567890123456789", result.Post.RemoteId);
        Assert.Equal("vhogemann", result.Post.AuthorHandle);
        Assert.Equal("https://x.com/vhogemann/status/1234567890123456789", result.Post.Url);
        Assert.True(result.Post.IsExternal);
    }

    private sealed class FakeMetaThreadsApiClient : SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsApiClient
    {
        private readonly Func<string, Task<SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsUserThreadsResponse>> _handler;

        public FakeMetaThreadsApiClient(Func<string, Task<SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsUserThreadsResponse>> handler)
            : base(new System.Net.Http.HttpClient(), Microsoft.Extensions.Options.Options.Create(new SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsOptions()), NullLogger<SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsApiClient>.Instance)
        {
            _handler = handler;
        }

        public override Task<SocialWorker.Api.Features.Publishing.MetaThreads.MetaThreadsUserThreadsResponse> GetUserThreadsAsync(string accessToken, int limit = 50, CancellationToken ct = default)
        {
            return _handler(accessToken);
        }
    }
}
