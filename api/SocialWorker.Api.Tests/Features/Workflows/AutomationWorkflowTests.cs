using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class AutomationWorkflowTests : SqliteTestBase
{
    [Fact]
    public void Create_ThrowsArgumentException_WhenTitleIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            AutomationWorkflow.Create(Guid.NewGuid(), "   ", "Some prompt"));
    }

    [Fact]
    public void Create_UsesDefaultPrompt_WhenPromptIsNull()
    {
        var wf = AutomationWorkflow.Create(Guid.NewGuid(), "Tech Summary", null);

        Assert.Equal("Tech Summary", wf.Title);
        Assert.Equal("Summarize this article as a thread.", wf.InstructionPrompt);
        Assert.NotEqual(Guid.Empty, wf.Id);
    }

    [Fact]
    public void Destination_Create_ThrowsArgumentException_WhenPlatformIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            AutomationWorkflowDestination.Create(Guid.NewGuid(), "   ", true));
    }

    [Fact]
    public void Destination_Create_InitializesPropertiesCorrectly()
    {
        var wfId = Guid.NewGuid();
        var dest = AutomationWorkflowDestination.Create(wfId, "Bluesky", true);

        Assert.Equal(wfId, dest.AutomationWorkflowId);
        Assert.Equal("Bluesky", dest.Platform);
        Assert.True(dest.AutoPublish);
        Assert.NotEqual(Guid.Empty, dest.Id);
    }

    [Fact]
    public async Task AppDbContext_SavesAndLoads_WorkflowGraphWithManySourcesAndDestinations()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed1 = FeedSubscription.Create(user.Id, "Feed 1", "https://example.com/feed1.xml");
        var feed2 = FeedSubscription.Create(user.Id, "Feed 2", "https://example.com/feed2.xml");
        db.FeedSubscriptions.AddRange(feed1, feed2);
        await db.SaveChangesAsync();

        var workflow = AutomationWorkflow.Create(user.Id, "Cross-Post Workflow", "Write snappy threads.");
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var sourceLink1 = AutomationWorkflowSource.Create(workflow.Id, feed1.Id);
        var sourceLink2 = AutomationWorkflowSource.Create(workflow.Id, feed2.Id);
        db.AutomationWorkflowSources.AddRange(sourceLink1, sourceLink2);

        var dest1 = AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", true);
        var dest2 = AutomationWorkflowDestination.Create(workflow.Id, "LinkedIn", false);
        db.AutomationWorkflowDestinations.AddRange(dest1, dest2);

        var draft = Draft.Create(user.Id, "Automated Post", "Content", null, workflow.Id);
        db.Drafts.Add(draft);

        var queueItem = FeedIngestionQueueItem.Create(
            feed1.Id,
            workflow.Id,
            "Item 1",
            "https://example.com/item1");
        db.FeedIngestionQueueItems.Add(queueItem);

        await db.SaveChangesAsync();

        // Query graph back from database
        var loaded = await db.AutomationWorkflows
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
            .Include(w => w.Drafts)
            .FirstOrDefaultAsync(w => w.Id == workflow.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Cross-Post Workflow", loaded.Title);
        Assert.Equal(2, loaded.WorkflowSources.Count);
        Assert.Contains(loaded.WorkflowSources, s => s.FeedSubscription.Title == "Feed 1");
        Assert.Contains(loaded.WorkflowSources, s => s.FeedSubscription.Title == "Feed 2");
        Assert.Equal(2, loaded.Destinations.Count);
        Assert.Contains(loaded.Destinations, d => d.Platform == "Bluesky" && d.AutoPublish);
        Assert.Contains(loaded.Destinations, d => d.Platform == "LinkedIn" && !d.AutoPublish);
        Assert.Single(loaded.Drafts);
        Assert.Equal(draft.Id, loaded.Drafts.First().Id);

        var loadedQueueItem = await db.FeedIngestionQueueItems
            .Include(q => q.AutomationWorkflow)
            .FirstOrDefaultAsync(q => q.Id == queueItem.Id);

        Assert.NotNull(loadedQueueItem);
        Assert.Equal(workflow.Id, loadedQueueItem.AutomationWorkflowId);
        Assert.Equal("Cross-Post Workflow", loadedQueueItem.AutomationWorkflow.Title);
    }
}
