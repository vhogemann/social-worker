using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Workflows;
using Xunit;


namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class WorkflowsEndpointTests : SqliteTestBase
{
    [Fact]
    public async Task CreateWorkflow_PersistsSourcesAndDestinations()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed1 = FeedSubscription.Create(user.Id, "Feed 1", "https://example.com/feed1.xml");
        db.FeedSubscriptions.Add(feed1);
        await db.SaveChangesAsync();

        var workflow = AutomationWorkflow.Create(user.Id, "Daily Digest", "Summarize in 3 tweets.");
        workflow.WorkflowSources.Add(AutomationWorkflowSource.Create(workflow.Id, feed1.Id));
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", true));
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "LinkedIn", false));

        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var loaded = await db.AutomationWorkflows
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
            .FirstOrDefaultAsync(w => w.Id == workflow.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Daily Digest", loaded.Title);
        Assert.Equal("Summarize in 3 tweets.", loaded.InstructionPrompt);
        Assert.Single(loaded.WorkflowSources);
        Assert.Equal(feed1.Id, loaded.WorkflowSources.First().FeedSubscriptionId);
        Assert.Equal(2, loaded.Destinations.Count);
        Assert.Contains(loaded.Destinations, d => d.Platform == "Bluesky" && d.AutoPublish);
        Assert.Contains(loaded.Destinations, d => d.Platform == "LinkedIn" && !d.AutoPublish);

        var dto = new AutomationWorkflowDto(loaded);
        Assert.Equal(workflow.Id, dto.Id);
        Assert.Single(dto.Sources);
        Assert.Equal("Feed 1", dto.Sources[0].Title);
        Assert.Equal(2, dto.Destinations.Count);
    }

    [Fact]
    public async Task UpdateWorkflow_ReplacesSourcesAndDestinations()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed1 = FeedSubscription.Create(user.Id, "Feed 1", "https://example.com/feed1.xml");
        var feed2 = FeedSubscription.Create(user.Id, "Feed 2", "https://example.com/feed2.xml");
        db.FeedSubscriptions.AddRange(feed1, feed2);
        await db.SaveChangesAsync();

        var workflow = AutomationWorkflow.Create(user.Id, "Initial Title", "Initial Prompt");
        workflow.WorkflowSources.Add(AutomationWorkflowSource.Create(workflow.Id, feed1.Id));
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", false));
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        // Update workflow
        var toUpdate = await db.AutomationWorkflows.FirstAsync(w => w.Id == workflow.Id);
        toUpdate.Title = "Updated Title";
        toUpdate.InstructionPrompt = "Updated Prompt";

        var existingSources = await db.AutomationWorkflowSources
            .Where(s => s.AutomationWorkflowId == workflow.Id)
            .ToListAsync();
        db.AutomationWorkflowSources.RemoveRange(existingSources);
        db.AutomationWorkflowSources.Add(AutomationWorkflowSource.Create(workflow.Id, feed2.Id));

        var existingDestinations = await db.AutomationWorkflowDestinations
            .Where(d => d.AutomationWorkflowId == workflow.Id)
            .ToListAsync();
        db.AutomationWorkflowDestinations.RemoveRange(existingDestinations);
        db.AutomationWorkflowDestinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "LinkedIn", true));

        await db.SaveChangesAsync();

        var updated = await db.AutomationWorkflows
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
            .FirstAsync(w => w.Id == workflow.Id);

        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Prompt", updated.InstructionPrompt);
        Assert.Single(updated.WorkflowSources);
        Assert.Equal(feed2.Id, updated.WorkflowSources.First().FeedSubscriptionId);
        Assert.Single(updated.Destinations);
        Assert.Equal("LinkedIn", updated.Destinations.First().Platform);
        Assert.True(updated.Destinations.First().AutoPublish);
    }

    [Fact]
    public async Task DeleteWorkflow_CascadesAssociations_AndSetsDraftWorkflowIdToNull()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed1 = FeedSubscription.Create(user.Id, "Feed 1", "https://example.com/feed1.xml");
        db.FeedSubscriptions.Add(feed1);
        await db.SaveChangesAsync();

        var workflow = AutomationWorkflow.Create(user.Id, "To Delete", "Prompt");
        workflow.WorkflowSources.Add(AutomationWorkflowSource.Create(workflow.Id, feed1.Id));
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", true));
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var draft = Draft.Create(user.Id, "Draft from Workflow", "Content", null, workflow.Id);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        // Delete workflow
        db.AutomationWorkflows.Remove(workflow);
        await db.SaveChangesAsync();

        Assert.Null(await db.AutomationWorkflows.FindAsync(workflow.Id));
        Assert.Empty(await db.AutomationWorkflowSources.Where(s => s.AutomationWorkflowId == workflow.Id).ToListAsync());
        Assert.Empty(await db.AutomationWorkflowDestinations.Where(d => d.AutomationWorkflowId == workflow.Id).ToListAsync());

        var loadedDraft = await db.Drafts.FindAsync(draft.Id);
        Assert.NotNull(loadedDraft);
        Assert.Null(loadedDraft.AutomationWorkflowId);
    }

    [Theory]
    [InlineData("Manual", WorkflowPublishingMode.Manual)]
    [InlineData("Automatic", WorkflowPublishingMode.Automatic)]
    [InlineData("Scheduled", WorkflowPublishingMode.Scheduled)]
    public void Deserialize_CreateAutomationWorkflowRequest_WithPublishingModeString_Succeeds(string modeString, WorkflowPublishingMode expectedMode)
    {
        var json = $$"""
        {
            "title": "Scheduled RSS Workflow",
            "instructionPrompt": "Summarize feed",
            "feedSubscriptionIds": [],
            "destinations": [
                {
                    "platform": "Bluesky",
                    "autoPublish": false,
                    "publishingMode": "{{modeString}}",
                    "scheduleId": "88937987-460a-4957-be7a-28c7651ac346"
                }
            ]
        }
        """;

        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var result = System.Text.Json.JsonSerializer.Deserialize<CreateAutomationWorkflowRequest>(json, options);

        Assert.NotNull(result);
        Assert.NotNull(result.Destinations);
        Assert.Single(result.Destinations);
        Assert.Equal(expectedMode, result.Destinations[0].PublishingMode);
    }
}

