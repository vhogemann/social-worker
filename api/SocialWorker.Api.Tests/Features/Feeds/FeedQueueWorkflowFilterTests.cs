using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedQueueWorkflowFilterTests : SqliteTestBase
{
    [Fact]
    public async Task QueueFilter_WithWorkflowId_ReturnsOnlyItemsForThatWorkflow()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "Feed", "https://example.com/rss");
        db.FeedSubscriptions.Add(feed);
        await db.SaveChangesAsync();

        var wf1 = AutomationWorkflow.Create(user.Id, "WF 1", "Prompt 1");
        var wf2 = AutomationWorkflow.Create(user.Id, "WF 2", "Prompt 2");
        db.AutomationWorkflows.AddRange(wf1, wf2);
        await db.SaveChangesAsync();

        var q1 = new FeedIngestionQueueItem
        {
            FeedSubscriptionId = feed.Id,
            AutomationWorkflowId = wf1.Id,
            ItemTitle = "Article 1",
            ItemLink = "https://example.com/1"
        };
        var q2 = new FeedIngestionQueueItem
        {
            FeedSubscriptionId = feed.Id,
            AutomationWorkflowId = wf1.Id,
            ItemTitle = "Article 2",
            ItemLink = "https://example.com/2"
        };
        var q3 = new FeedIngestionQueueItem
        {
            FeedSubscriptionId = feed.Id,
            AutomationWorkflowId = wf2.Id,
            ItemTitle = "Article 3",
            ItemLink = "https://example.com/3"
        };
        db.FeedIngestionQueueItems.AddRange(q1, q2, q3);
        await db.SaveChangesAsync();

        // Query with wf1 filter
        var wf1Items = await db.FeedIngestionQueueItems
            .Where(q => q.FeedSubscription.UserId == user.Id && q.AutomationWorkflowId == wf1.Id)
            .ToListAsync();

        Assert.Equal(2, wf1Items.Count);
        Assert.All(wf1Items, item => Assert.Equal(wf1.Id, item.AutomationWorkflowId));

        // Query with wf2 filter
        var wf2Items = await db.FeedIngestionQueueItems
            .Where(q => q.FeedSubscription.UserId == user.Id && q.AutomationWorkflowId == wf2.Id)
            .ToListAsync();

        Assert.Single(wf2Items);
        Assert.Equal("Article 3", wf2Items[0].ItemTitle);

        // Query without filter
        var allItems = await db.FeedIngestionQueueItems
            .Where(q => q.FeedSubscription.UserId == user.Id)
            .ToListAsync();

        Assert.Equal(3, allItems.Count);
    }
}
