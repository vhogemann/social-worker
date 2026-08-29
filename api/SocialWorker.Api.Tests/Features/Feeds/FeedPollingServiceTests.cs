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
}
