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
using SocialWorker.Api.Features.Publishing;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedAutoPublishServiceTests : SqliteTestBase
{
    private sealed class StubPublisher : IPublisher
    {
        public string Platform => "Bluesky";

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            return Task.FromResult(new PublishResult
            {
                Success = true,
                Posts = new List<PublishedPost>
                {
                    new PublishedPost
                    {
                        SegmentIndex = 0,
                        RemoteId = "at://did:plc:123/app.bsky.feed.post/456",
                        Url = "https://bsky.app/profile/user/post/456"
                    }
                }
            });
        }
    }

    [Fact]
    public async Task PublishDestinations_SkipsWhenAutoPublishIsFalse()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Draft");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var resolver = new PublisherResolver(new[] { new StubPublisher() });
        var service = new FeedAutoPublishService(db, resolver, NullLogger<FeedAutoPublishService>.Instance);

        var destinations = new List<AutomationWorkflowDestination>
        {
            new AutomationWorkflowDestination { Platform = "Bluesky", AutoPublish = false }
        };

        await service.PublishDestinationsAsync(draft, destinations, user.Id, CancellationToken.None);

        Assert.Empty(await db.Posts.ToListAsync());
    }

    [Fact]
    public async Task PublishDestinations_PublishesWhenEnabledAndAccountExists()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Bluesky",
            Handle = "user.bsky.social",
            Status = "Active"
        };
        db.Accounts.Add(account);

        var draft = Draft.Create(user.Id, "Draft to Publish");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var thread = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "Hello from automated post!"
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var resolver = new PublisherResolver(new[] { new StubPublisher() });
        var service = new FeedAutoPublishService(db, resolver, NullLogger<FeedAutoPublishService>.Instance);

        var destinations = new List<AutomationWorkflowDestination>
        {
            new AutomationWorkflowDestination { Platform = "Bluesky", AutoPublish = true }
        };

        await service.PublishDestinationsAsync(draft, destinations, user.Id, CancellationToken.None);

        var posts = await db.Posts.ToListAsync();
        Assert.Single(posts);
        Assert.Equal("Bluesky", posts[0].Platform);
        Assert.Equal("https://bsky.app/profile/user/post/456", posts[0].Url);

        var updatedThread = await db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);
    }
}
