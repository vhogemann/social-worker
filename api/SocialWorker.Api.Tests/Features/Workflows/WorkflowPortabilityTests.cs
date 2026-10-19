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

public sealed class WorkflowPortabilityTests : SqliteTestBase
{
    [Fact]
    public async Task ExportAsync_StripsSensitiveData_PreservesFeedDefinitions()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        // Account with sensitive tokens
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Twitter",
            Handle = "@techlead",
            CredentialsEncrypted = "encrypted-secret-blob",
            Status = "Active"
        };
        db.Accounts.Add(account);

        var feed = FeedSubscription.Create(
            user.Id,
            "AI Research News",
            "https://arxiv.org/rss/cs.AI",
            "https://arxiv.org",
            "LLM,Transformer",
            "RecSys");
        db.FeedSubscriptions.Add(feed);

        var workflow = AutomationWorkflow.Create(user.Id, "Arxiv Tech Digest", "Summarize in 4 tweets", null, 8);
        workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, feed.Id));
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "Twitter", WorkflowPublishingMode.Automatic, null, account.Id));
        workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, "Bluesky", WorkflowPublishingMode.Manual, null, null));
        db.AutomationWorkflows.Add(workflow);

        await db.SaveChangesAsync();

        var service = new WorkflowPortabilityService(db);
        var export = await service.ExportAsync(user.Id, workflow.Id);

        Assert.Equal(1, export.Version);
        Assert.Single(export.Workflows);

        var exported = export.Workflows[0];
        Assert.Equal("Arxiv Tech Digest", exported.Title);
        Assert.Equal("Summarize in 4 tweets", exported.InstructionPrompt);
        Assert.Equal(8, exported.MinRelevanceScore);

        // Verify Source contains full feed definition
        Assert.Single(exported.Sources);
        Assert.Equal(WorkflowSourceKind.Feed, exported.Sources[0].Kind);
        Assert.NotNull(exported.Sources[0].Feed);
        Assert.Equal("AI Research News", exported.Sources[0].Feed!.Title);
        Assert.Equal("https://arxiv.org/rss/cs.AI", exported.Sources[0].Feed!.FeedUrl);
        Assert.Equal("https://arxiv.org", exported.Sources[0].Feed!.WebsiteUrl);
        Assert.Equal("LLM,Transformer", exported.Sources[0].Feed!.IncludeFilters);
        Assert.Equal("RecSys", exported.Sources[0].Feed!.ExcludeFilters);

        // Verify Destinations have platform & publishing mode, but NO account or token references
        Assert.Equal(2, exported.Destinations.Count);
        Assert.Contains(exported.Destinations, d => d.Platform == "Twitter" && d.PublishingMode == WorkflowPublishingMode.Automatic && d.AutoPublish);
        Assert.Contains(exported.Destinations, d => d.Platform == "Bluesky" && d.PublishingMode == WorkflowPublishingMode.Manual && !d.AutoPublish);
    }

    [Fact]
    public async Task ImportAsync_CreatesNewWorkflowsAndFeeds()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var package = new WorkflowExportPackage(
            1,
            DateTimeOffset.UtcNow,
            new List<ExportedWorkflow>
            {
                new(
                    "Robotics Digest",
                    "Write an insightful thread about robotics developments.",
                    7,
                    new List<ExportedWorkflowSource>
                    {
                        new(
                            WorkflowSourceKind.Feed,
                            new ExportedFeedDefinition(
                                "IEEE Spectrum",
                                "https://spectrum.ieee.org/rss/robotics/fulltext",
                                "https://spectrum.ieee.org",
                                "Humanoid",
                                "Drone")
                        )
                    },
                    new List<ExportedWorkflowDestination>
                    {
                        new("Bluesky", false, WorkflowPublishingMode.Manual)
                    }
                )
            });

        var service = new WorkflowPortabilityService(db);
        var result = await service.ImportAsync(user.Id, package);

        Assert.Equal(1, result.ImportedWorkflowsCount);
        Assert.Equal(1, result.CreatedFeedsCount);
        Assert.Equal(0, result.ReusedFeedsCount);
        Assert.Single(result.ImportedWorkflowIds);

        // Verify feed was saved in DB
        var savedFeed = await db.FeedSubscriptions.FirstOrDefaultAsync(f => f.FeedUrl == "https://spectrum.ieee.org/rss/robotics/fulltext");
        Assert.NotNull(savedFeed);
        Assert.Equal(user.Id, savedFeed.UserId);
        Assert.Equal("IEEE Spectrum", savedFeed.Title);
        Assert.Equal("Humanoid", savedFeed.IncludeFilters);

        // Verify workflow was saved in DB
        var savedWf = await db.AutomationWorkflows
            .Include(w => w.WorkflowSources)
            .Include(w => w.Destinations)
            .FirstOrDefaultAsync(w => w.Id == result.ImportedWorkflowIds[0]);

        Assert.NotNull(savedWf);
        Assert.Equal("Robotics Digest", savedWf.Title);
        Assert.Equal(user.Id, savedWf.UserId);
        Assert.Single(savedWf.WorkflowSources);
        Assert.Equal(savedFeed.Id, savedWf.WorkflowSources.First().FeedSubscriptionId);
        Assert.Single(savedWf.Destinations);
        Assert.Equal("Bluesky", savedWf.Destinations.First().Platform);
        Assert.Null(savedWf.Destinations.First().AccountId);
    }

    [Fact]
    public async Task ImportAsync_DeduplicatesExistingFeeds()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        // Pre-existing feed subscription
        var existingFeed = FeedSubscription.Create(user.Id, "Pre-existing TechCrunch", "https://techcrunch.com/feed/");
        db.FeedSubscriptions.Add(existingFeed);
        await db.SaveChangesAsync();

        var package = new WorkflowExportPackage(
            1,
            DateTimeOffset.UtcNow,
            new List<ExportedWorkflow>
            {
                new(
                    "TechCrunch Highlights",
                    "Summarize startups.",
                    7,
                    new List<ExportedWorkflowSource>
                    {
                        new(
                            WorkflowSourceKind.Feed,
                            new ExportedFeedDefinition(
                                "TechCrunch (New Name)",
                                "https://techcrunch.com/feed/", // Identical URL
                                "https://techcrunch.com")
                        )
                    },
                    new List<ExportedWorkflowDestination>
                    {
                        new("LinkedIn", false, WorkflowPublishingMode.Manual)
                    }
                )
            });

        var service = new WorkflowPortabilityService(db);
        var result = await service.ImportAsync(user.Id, package);

        Assert.Equal(1, result.ImportedWorkflowsCount);
        Assert.Equal(0, result.CreatedFeedsCount);
        Assert.Equal(1, result.ReusedFeedsCount);

        // Confirm only 1 feed subscription exists in total
        Assert.Equal(1, await db.FeedSubscriptions.CountAsync(f => f.UserId == user.Id));

        var wf = await db.AutomationWorkflows
            .Include(w => w.WorkflowSources)
            .FirstAsync(w => w.Id == result.ImportedWorkflowIds[0]);

        Assert.Equal(existingFeed.Id, wf.WorkflowSources.First().FeedSubscriptionId);
    }

    [Fact]
    public async Task ExportImport_Roundtrip_PreservesConfiguration()
    {
        using var db = CreateDbContext();
        var userA = CreateSeedUser(db);
        var userB = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "userB",
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(userB);

        var feed = FeedSubscription.Create(userA.Id, "Original Feed", "https://news.ycombinator.com/rss", "https://news.ycombinator.com", "Show HN", "Ask HN");
        db.FeedSubscriptions.Add(feed);

        var wfA = AutomationWorkflow.Create(userA.Id, "Hacker News Digest", "Extract top 3 discussions.", null, 9);
        wfA.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(wfA.Id, feed.Id));
        wfA.Destinations.Add(AutomationWorkflowDestination.Create(wfA.Id, "Bluesky", WorkflowPublishingMode.Manual));
        wfA.Destinations.Add(AutomationWorkflowDestination.Create(wfA.Id, "Twitter", WorkflowPublishingMode.Automatic));
        db.AutomationWorkflows.Add(wfA);
        await db.SaveChangesAsync();

        var service = new WorkflowPortabilityService(db);
        var export = await service.ExportAsync(userA.Id);

        // Import into User B
        var importResult = await service.ImportAsync(userB.Id, export);
        Assert.Equal(1, importResult.ImportedWorkflowsCount);
        Assert.Equal(1, importResult.CreatedFeedsCount);

        // Re-export from User B
        var exportB = await service.ExportAsync(userB.Id, importResult.ImportedWorkflowIds[0]);

        Assert.Single(exportB.Workflows);
        var wfB = exportB.Workflows[0];
        Assert.Equal(wfA.Title, wfB.Title);
        Assert.Equal(wfA.InstructionPrompt, wfB.InstructionPrompt);
        Assert.Equal(wfA.MinRelevanceScore, wfB.MinRelevanceScore);
        Assert.Equal(feed.FeedUrl, wfB.Sources[0].Feed!.FeedUrl);
        Assert.Equal(feed.IncludeFilters, wfB.Sources[0].Feed!.IncludeFilters);
        Assert.Equal(feed.ExcludeFilters, wfB.Sources[0].Feed!.ExcludeFilters);
        Assert.Equal(2, wfB.Destinations.Count);
    }
}
