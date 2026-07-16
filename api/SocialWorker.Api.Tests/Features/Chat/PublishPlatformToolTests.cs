using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class PublishPlatformToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNoDraftId()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var tool = new PublishPlatformTool(sp.GetRequiredService<IServiceScopeFactory>());

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), null, Guid.NewGuid(), CancellationToken.None);

        var error = result.GetType().GetProperty("error")?.GetValue(result) as string;
        Assert.Contains("No active draft", error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenReplyTargetRevalidationFails()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var threadId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing });
        db.PlatformThreads.Add(new PlatformThread
        {
            Id = threadId,
            DraftId = draftId,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "Hello world"
        });
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "Bluesky",
            Handle = "user.bsky.social",
            CredentialsEncrypted = "encrypted"
        });
        db.DraftBlueskyMetadata.Add(new DraftBlueskyMetadata
        {
            DraftId = draftId,
            ReplyRootUri = "at://did:plc:root/app.bsky.feed.post/1",
            ReplyRootCid = "root-cid",
            ReplyParentUri = "at://did:plc:parent/app.bsky.feed.post/2",
            ReplyParentCid = "parent-cid",
            ReplyParentUrl = "https://bsky.app/profile/example/post/2"
        });
        await db.SaveChangesAsync();

        var publisher = new StubPublisher();
        var resolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(false, "post not found"));
        var tool = BuildTool(db, publisher, resolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), draftId, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("revalidated", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, publisher.CallCount);
    }

    private static PublishPlatformTool BuildTool(AppDbContext db, StubPublisher publisher, IBlueskyReplyTargetResolver resolver)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<BlueskyContentValidator>();
        services.AddSingleton<IPublisherResolver>(new StubPublisherResolver(publisher));
        services.AddSingleton<IBlueskyReplyTargetResolver>(resolver);
        return new PublishPlatformTool(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class StubPublisherResolver : IPublisherResolver
    {
        private readonly IPublisher _publisher;

        public StubPublisherResolver(IPublisher publisher)
        {
            _publisher = publisher;
        }

        public IPublisher Resolve(string platform) => _publisher;
    }

    private sealed class StubPublisher : IPublisher
    {
        public string Platform => "Bluesky";
        public int CallCount { get; private set; }

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new PublishResult
            {
                Success = true,
                Posts = new List<PublishedPost>
                {
                    new() { SegmentIndex = 0, RemoteId = "remote", Url = "https://bsky.app/profile/user/post/1" }
                }
            });
        }
    }

    private sealed class StubReplyTargetResolver : IBlueskyReplyTargetResolver
    {
        private readonly BlueskyReplyTargetResolutionResult _result;

        public StubReplyTargetResolver(BlueskyReplyTargetResolutionResult result)
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
}