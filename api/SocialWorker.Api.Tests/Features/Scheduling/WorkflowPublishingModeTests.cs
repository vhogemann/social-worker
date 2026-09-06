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
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class WorkflowPublishingModeTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly StubPublisher _publisher;
    private readonly StubPublisherResolver _resolver;
    private readonly TestBackgroundTaskQueue _taskQueue;
    private readonly ScheduleQueueService _queueService;
    private readonly ScheduleSlotEvaluatorService _evaluator;
    private readonly FeedAutoPublishService _autoPublishService;

    public WorkflowPublishingModeTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
        _publisher = new StubPublisher();
        _resolver = new StubPublisherResolver(_publisher);
        _taskQueue = new TestBackgroundTaskQueue();
        _queueService = new ScheduleQueueService(_db, _taskQueue);
        _evaluator = new ScheduleSlotEvaluatorService(_db, _taskQueue);

        _autoPublishService = new FeedAutoPublishService(
            _db,
            _resolver,
            NullLogger<FeedAutoPublishService>.Instance,
            _queueService,
            _evaluator);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    private async Task<(Draft Draft, PlatformThread Thread, Account Account)> CreateTestEntitiesAsync()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Platform = "Bluesky",
            Handle = "test.bsky.social",
            CredentialsEncrypted = "encrypted"
        };
        _db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Automated Draft",
            Content = "High quality thread content"
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            AccountId = account.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "High quality thread content"
        };
        _db.PlatformThreads.Add(thread);

        await _db.SaveChangesAsync();
        return (draft, thread, account);
    }

    [Fact]
    public async Task PublishDestinationsAsync_ManualMode_LeavesDraftForReview()
    {
        var (draft, thread, _) = await CreateTestEntitiesAsync();

        var destinations = new List<AutomationWorkflowDestination>
        {
            AutomationWorkflowDestination.Create(Guid.NewGuid(), "Bluesky", WorkflowPublishingMode.Manual)
        };

        await _autoPublishService.PublishDestinationsAsync(draft, destinations, _user.Id, CancellationToken.None);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Draft, updatedThread.Stage);
        Assert.False(_publisher.WasCalled);
        Assert.Empty(_taskQueue.EnqueuedTasks);
    }

    [Fact]
    public async Task PublishDestinationsAsync_AutomaticMode_ImmediatelyPublishes()
    {
        var (draft, thread, _) = await CreateTestEntitiesAsync();

        var destinations = new List<AutomationWorkflowDestination>
        {
            AutomationWorkflowDestination.Create(Guid.NewGuid(), "Bluesky", WorkflowPublishingMode.Automatic)
        };

        await _autoPublishService.PublishDestinationsAsync(draft, destinations, _user.Id, CancellationToken.None);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);
        Assert.True(_publisher.WasCalled);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(posts);
    }

    [Fact]
    public async Task PublishDestinationsAsync_ScheduledMode_EnqueuesIntoSchedule()
    {
        var (draft, thread, _) = await CreateTestEntitiesAsync();

        var schedule = Schedule.Create(_user.Id, "Daily Buffer");
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        var destinations = new List<AutomationWorkflowDestination>
        {
            AutomationWorkflowDestination.Create(Guid.NewGuid(), "Bluesky", WorkflowPublishingMode.Scheduled, schedule.Id)
        };

        await _autoPublishService.PublishDestinationsAsync(draft, destinations, _user.Id, CancellationToken.None);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Scheduled, updatedThread.Stage);

        var queueItems = await _db.ScheduleQueueItems.Where(q => q.ScheduleId == schedule.Id).ToListAsync();
        Assert.Single(queueItems);
        Assert.Equal(draft.Id, queueItems[0].DraftId);
        Assert.Equal(thread.Id, queueItems[0].PlatformThreadId);
    }

    private sealed class StubPublisherResolver : IPublisherResolver
    {
        private readonly IPublisher _publisher;
        public StubPublisherResolver(IPublisher publisher) => _publisher = publisher;
        public IPublisher? Resolve(string platform) => _publisher;
    }

    private sealed class StubPublisher : IPublisher
    {
        public string Platform => "Bluesky";
        public bool WasCalled { get; private set; }

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new PublishResult
            {
                Success = true,
                Posts = new List<PublishedPost>
                {
                    new() { SegmentIndex = 0, RemoteId = "uri", Url = "https://bsky.app" }
                }
            });
        }
    }
}
