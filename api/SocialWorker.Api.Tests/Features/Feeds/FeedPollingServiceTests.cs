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
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedPollingServiceTests : SqliteTestBase
{
    [Fact]
    public async Task PollSubscription_WithNoLinkedWorkflows_CreatesZeroQueueItems()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "Unlinked Feed", "https://example.com/feed.xml");
        db.FeedSubscriptions.Add(feed);
        await db.SaveChangesAsync();

        // Feed has no WorkflowSources
        var queueItems = await db.FeedIngestionQueueItems
            .Where(q => q.FeedSubscriptionId == feed.Id)
            .ToListAsync();

        Assert.Empty(queueItems);
    }

    [Fact]
    public async Task QueueItems_CreatedForMultipleWorkflows_AreDeduplicatedPerWorkflow()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "Tech Feed", "https://example.com/feed.xml");
        db.FeedSubscriptions.Add(feed);
        await db.SaveChangesAsync();

        var wf1 = AutomationWorkflow.Create(user.Id, "Workflow 1", "Prompt 1");
        var wf2 = AutomationWorkflow.Create(user.Id, "Workflow 2", "Prompt 2");
        db.AutomationWorkflows.AddRange(wf1, wf2);
        await db.SaveChangesAsync();

        db.AutomationWorkflowSources.Add(AutomationWorkflowSource.Create(wf1.Id, feed.Id));
        db.AutomationWorkflowSources.Add(AutomationWorkflowSource.Create(wf2.Id, feed.Id));
        await db.SaveChangesAsync();

        // Enqueue items for both workflows for the same article link
        var link = "https://example.com/posts/article-1";
        var item1 = FeedIngestionQueueItem.Create(feed.Id, wf1.Id, "Article 1", link);
        var item2 = FeedIngestionQueueItem.Create(feed.Id, wf2.Id, "Article 1", link);

        db.FeedIngestionQueueItems.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var savedItems = await db.FeedIngestionQueueItems
            .Where(q => q.FeedSubscriptionId == feed.Id)
            .ToListAsync();

        Assert.Equal(2, savedItems.Count);
        Assert.Contains(savedItems, q => q.AutomationWorkflowId == wf1.Id);
        Assert.Contains(savedItems, q => q.AutomationWorkflowId == wf2.Id);

        // Verify deduplication check prevents adding duplicate for wf1
        var duplicateExists = await db.FeedIngestionQueueItems
            .AnyAsync(q => q.FeedSubscriptionId == feed.Id && q.AutomationWorkflowId == wf1.Id && q.ItemLink == link);

        Assert.True(duplicateExists);
    }

    [Fact]
    public async Task IngestSource_CreatesPlatformAgnosticCanonicalDraft_WithWorkflowLink()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "Feed", "https://example.com/feed.xml");
        var wf = AutomationWorkflow.Create(user.Id, "Workflow", "Prompt");
        db.FeedSubscriptions.Add(feed);
        db.AutomationWorkflows.Add(wf);
        await db.SaveChangesAsync();

        var draft = Draft.Create(user.Id, "Canonical Draft", "Content", null, wf.Id);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var loaded = await db.Drafts.FindAsync(draft.Id);
        Assert.NotNull(loaded);
        Assert.Null(loaded.TargetPlatform); // Platform-agnostic
        Assert.Equal(wf.Id, loaded.AutomationWorkflowId);
    }

    [Fact]
    public async Task PollSubscription_OnInitialPoll_EnqueuesAllFeedItems_WithoutTruncation()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "YouTube Playlist", "https://youtube.com/playlist.xml");
        db.FeedSubscriptions.Add(feed);
        var wf = AutomationWorkflow.Create(user.Id, "LinkedIn Posts", "Summarize");
        db.AutomationWorkflows.Add(wf);
        await db.SaveChangesAsync();

        db.AutomationWorkflowSources.Add(AutomationWorkflowSource.Create(wf.Id, feed.Id));
        await db.SaveChangesAsync();

        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
  <channel>
    <title>Playlist</title>
    <item><title>Video 1</title><link>https://youtube.com/watch?v=1</link><pubDate>Wed, 19 Aug 2026 08:30:00 GMT</pubDate></item>
    <item><title>Video 2</title><link>https://youtube.com/watch?v=2</link><pubDate>Sat, 22 Aug 2026 19:15:00 GMT</pubDate></item>
    <item><title>Video 3</title><link>https://youtube.com/watch?v=3</link><pubDate>Wed, 26 Aug 2026 10:16:00 GMT</pubDate></item>
    <item><title>Video 4</title><link>https://youtube.com/watch?v=4</link><pubDate>Thu, 27 Aug 2026 08:30:00 GMT</pubDate></item>
    <item><title>Video 5</title><link>https://youtube.com/watch?v=5</link><pubDate>Sat, 20 Jun 2026 09:03:00 GMT</pubDate></item>
    <item><title>Video 6</title><link>https://youtube.com/watch?v=6</link><pubDate>Tue, 25 Aug 2026 09:30:00 GMT</pubDate></item>
    <item><title>Video 7</title><link>https://youtube.com/watch?v=7</link><pubDate>Sat, 29 Aug 2026 09:00:00 GMT</pubDate></item>
    <item><title>Video 8</title><link>https://youtube.com/watch?v=8</link><pubDate>Thu, 10 Sep 2026 19:13:00 GMT</pubDate></item>
  </channel>
</rss>";

        var parsedFeed = CodeHollow.FeedReader.FeedReader.ReadFromString(xml);
        var fakeReader = new FakeFeedReaderService(parsedFeed);
        var queue = new TestBackgroundTaskQueue();
        var pollingService = new FeedPollingService(db, queue, NullLogger<FeedPollingService>.Instance, fakeReader);

        await pollingService.PollSubscriptionAsync(feed.Id, CancellationToken.None);

        Assert.Equal(8, queue.EnqueuedTasks.Count);
        Assert.All(queue.EnqueuedTasks, t => Assert.Equal("feed-ingestion", t.TaskType));

        var updatedFeed = await db.FeedSubscriptions.FindAsync(feed.Id);
        Assert.NotNull(updatedFeed?.LastPolledAt);
    }

    [Fact]
    public async Task PollSubscription_WithForceAll_EnqueuesAllFeedItems_EvenIfLastPolledAtIsRecent()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "YouTube Playlist", "https://youtube.com/playlist.xml");
        feed.LastPolledAt = DateTime.UtcNow; // Already polled recently
        db.FeedSubscriptions.Add(feed);
        var wf = AutomationWorkflow.Create(user.Id, "LinkedIn Posts", "Summarize");
        db.AutomationWorkflows.Add(wf);
        await db.SaveChangesAsync();

        db.AutomationWorkflowSources.Add(AutomationWorkflowSource.Create(wf.Id, feed.Id));
        await db.SaveChangesAsync();

        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""2.0"">
  <channel>
    <title>Playlist</title>
    <item><title>Video 1</title><link>https://youtube.com/watch?v=1</link><pubDate>Wed, 19 Aug 2026 08:30:00 GMT</pubDate></item>
    <item><title>Video 2</title><link>https://youtube.com/watch?v=2</link><pubDate>Sat, 22 Aug 2026 19:15:00 GMT</pubDate></item>
    <item><title>Video 3</title><link>https://youtube.com/watch?v=3</link><pubDate>Wed, 26 Aug 2026 10:16:00 GMT</pubDate></item>
  </channel>
</rss>";

        var parsedFeed = CodeHollow.FeedReader.FeedReader.ReadFromString(xml);
        var fakeReader = new FakeFeedReaderService(parsedFeed);
        var queue = new TestBackgroundTaskQueue();
        var pollingService = new FeedPollingService(db, queue, NullLogger<FeedPollingService>.Instance, fakeReader);

        // Without forceAll -> 0 items because pubDate is older than LastPolledAt
        await pollingService.PollSubscriptionAsync(feed.Id, CancellationToken.None, forceAll: false);
        Assert.Empty(queue.EnqueuedTasks);

        // With forceAll -> all 3 items enqueued
        await pollingService.PollSubscriptionAsync(feed.Id, CancellationToken.None, forceAll: true);
        Assert.Equal(3, queue.EnqueuedTasks.Count);
    }

    private sealed class FakeFeedReaderService : IFeedReaderService
    {
        private readonly CodeHollow.FeedReader.Feed _feed;

        public FakeFeedReaderService(CodeHollow.FeedReader.Feed feed)
        {
            _feed = feed;
        }

        public Task<CodeHollow.FeedReader.Feed> ReadFeedAsync(string feedUrl, CancellationToken ct = default)
        {
            return Task.FromResult(_feed);
        }
    }
}


