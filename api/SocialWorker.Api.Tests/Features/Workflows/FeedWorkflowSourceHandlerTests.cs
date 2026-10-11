using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class FeedWorkflowSourceHandlerTests : SqliteTestBase
{
    private sealed class ThrowingFeedReader : IFeedReaderService
    {
        public Task<CodeHollow.FeedReader.Feed> ReadFeedAsync(string feedUrl, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Failed to read feed XML");
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenPollingThrows_LogsErrorAndReturnsZeroWithoutCrashing()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var feed = FeedSubscription.Create(user.Id, "Broken Feed", "https://example.com/broken.xml");
        db.FeedSubscriptions.Add(feed);
        await db.SaveChangesAsync();

        var workflow = AutomationWorkflow.Create(user.Id, "Workflow", "Instruction");
        var source = AutomationWorkflowSource.CreateFeedSource(workflow.Id, feed.Id);
        db.AutomationWorkflows.Add(workflow);
        db.AutomationWorkflowSources.Add(source);
        await db.SaveChangesAsync();

        var pollingService = new FeedPollingService(
            db,
            new TestBackgroundTaskQueue(),
            NullLogger<FeedPollingService>.Instance,
            new ThrowingFeedReader());

        var handler = new FeedWorkflowSourceHandler(
            pollingService,
            NullLogger<FeedWorkflowSourceHandler>.Instance);

        var result = await handler.ProcessAsync(workflow, source, CancellationToken.None);

        Assert.Equal(0, result);
    }
}
