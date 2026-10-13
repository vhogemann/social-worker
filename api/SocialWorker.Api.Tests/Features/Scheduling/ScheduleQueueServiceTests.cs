using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class ScheduleQueueServiceTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly TestBackgroundTaskQueue _taskQueue;
    private readonly ScheduleQueueService _service;

    public ScheduleQueueServiceTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
        _taskQueue = new TestBackgroundTaskQueue();
        _service = new ScheduleQueueService(_db, _taskQueue);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    private async Task<(Schedule Schedule, Draft Draft, PlatformThread Thread)> CreateSeedEntitiesAsync()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer");
        _db.Schedules.Add(schedule);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Draft",
            Content = "Post content"
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft
        };
        _db.PlatformThreads.Add(thread);

        await _db.SaveChangesAsync();
        return (schedule, draft, thread);
    }

    [Fact]
    public async Task EnqueueAsync_EmptyQueue_SetsPositionZeroAndScheduledStage()
    {
        var (schedule, draft, thread) = await CreateSeedEntitiesAsync();

        var item = await _service.EnqueueAsync(schedule.Id, thread.Id, draft.Id);

        Assert.Equal(0, item.Position);
        Assert.Equal(ScheduleQueueItemStatus.Queued, item.Status);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Scheduled, updatedThread.Stage);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleItems_AppendsIncrementingPositions()
    {
        var (schedule, draft1, thread1) = await CreateSeedEntitiesAsync();

        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 2", Content = "Content 2" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };
        _db.Drafts.Add(draft2);
        _db.PlatformThreads.Add(thread2);
        await _db.SaveChangesAsync();

        var item1 = await _service.EnqueueAsync(schedule.Id, thread1.Id, draft1.Id);
        var item2 = await _service.EnqueueAsync(schedule.Id, thread2.Id, draft2.Id);

        Assert.Equal(0, item1.Position);
        Assert.Equal(1, item2.Position);
    }

    [Fact]
    public async Task EnqueueAsync_InsertAtPosition_ShiftsExistingItems()
    {
        var (schedule, draft1, thread1) = await CreateSeedEntitiesAsync();
        var item1 = await _service.EnqueueAsync(schedule.Id, thread1.Id, draft1.Id);

        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 2", Content = "Content 2" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };
        _db.Drafts.Add(draft2);
        _db.PlatformThreads.Add(thread2);
        await _db.SaveChangesAsync();

        // Insert draft2 at position 0
        var item2 = await _service.EnqueueAsync(schedule.Id, thread2.Id, draft2.Id, targetPosition: 0);

        var items = await _service.GetQueueAsync(schedule.Id);
        Assert.Equal(2, items.Count);
        Assert.Equal(item2.Id, items[0].Id);
        Assert.Equal(0, items[0].Position);
        Assert.Equal(item1.Id, items[1].Id);
        Assert.Equal(1, items[1].Position);
    }

    [Fact]
    public async Task ReorderQueueAsync_UpdatesAllPositionsInOrder()
    {
        var (schedule, draft1, thread1) = await CreateSeedEntitiesAsync();
        var item1 = await _service.EnqueueAsync(schedule.Id, thread1.Id, draft1.Id);

        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 2", Content = "Content 2" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };
        _db.Drafts.Add(draft2);
        _db.PlatformThreads.Add(thread2);

        var draft3 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 3", Content = "Content 3" };
        var thread3 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft3.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };
        _db.Drafts.Add(draft3);
        _db.PlatformThreads.Add(thread3);
        await _db.SaveChangesAsync();

        var item2 = await _service.EnqueueAsync(schedule.Id, thread2.Id, draft2.Id);
        var item3 = await _service.EnqueueAsync(schedule.Id, thread3.Id, draft3.Id);

        // Reverse: [item3, item1, item2]
        var reordered = await _service.ReorderQueueAsync(schedule.Id, new[] { item3.Id, item1.Id, item2.Id });

        Assert.Equal(item3.Id, reordered[0].Id);
        Assert.Equal(0, reordered[0].Position);
        Assert.Equal(item1.Id, reordered[1].Id);
        Assert.Equal(1, reordered[1].Position);
        Assert.Equal(item2.Id, reordered[2].Id);
        Assert.Equal(2, reordered[2].Position);
    }

    [Fact]
    public async Task RemoveFromQueueAsync_CancelsTaskAndCompactsPositions()
    {
        var (schedule, draft1, thread1) = await CreateSeedEntitiesAsync();
        var item1 = await _service.EnqueueAsync(schedule.Id, thread1.Id, draft1.Id);

        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 2", Content = "Content 2" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };
        _db.Drafts.Add(draft2);
        _db.PlatformThreads.Add(thread2);
        await _db.SaveChangesAsync();

        var item2 = await _service.EnqueueAsync(schedule.Id, thread2.Id, draft2.Id);

        // Assign a mock scheduled task to item1
        var taskId = Guid.NewGuid();
        item1.ScheduledTaskId = taskId;
        await _db.SaveChangesAsync();

        // Remove item1
        var removed = await _service.RemoveFromQueueAsync(schedule.Id, item1.Id);

        Assert.True(removed);
        var queue = await _service.GetQueueAsync(schedule.Id);
        Assert.Single(queue);
        Assert.Equal(item2.Id, queue[0].Id);
        Assert.Equal(0, queue[0].Position);

        // Thread 1 stage should reset to Draft
        var updatedThread1 = await _db.PlatformThreads.FindAsync(thread1.Id);
        Assert.NotNull(updatedThread1);
        Assert.Equal(PlatformThreadStage.Draft, updatedThread1.Stage);
    }

    [Fact]
    public async Task PauseQueueItemAsync_PausesAndClearsSlots_UnpauseRestoresQueued()
    {
        var (schedule, draft, thread) = await CreateSeedEntitiesAsync();
        var item = await _service.EnqueueAsync(schedule.Id, thread.Id, draft.Id);

        item.AssignedSlotUtc = DateTime.UtcNow.AddHours(2);
        item.ScheduledTaskId = Guid.NewGuid();
        await _db.SaveChangesAsync();

        // Pause
        var paused = await _service.PauseQueueItemAsync(schedule.Id, item.Id, isPaused: true);
        Assert.NotNull(paused);
        Assert.Equal(ScheduleQueueItemStatus.Paused, paused.Status);
        Assert.Null(paused.AssignedSlotUtc);
        Assert.Null(paused.ScheduledTaskId);

        // Resume
        var resumed = await _service.PauseQueueItemAsync(schedule.Id, item.Id, isPaused: false);
        Assert.NotNull(resumed);
        Assert.Equal(ScheduleQueueItemStatus.Queued, resumed.Status);
    }

    [Fact]
    public async Task RemoveWorkflowItemsFromQueueAsync_RemovesOnlyMatchingWorkflowItemsAndReindexesRemaining()
    {
        var schedule = Schedule.Create(_user.Id, "Threads Queue");
        _db.Schedules.Add(schedule);

        var wf1 = new AutomationWorkflow { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Target WF", InstructionPrompt = "P" };
        var wf2 = new AutomationWorkflow { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Other WF", InstructionPrompt = "P" };
        _db.AutomationWorkflows.AddRange(wf1, wf2);

        var targetWorkflowId = wf1.Id;
        var otherWorkflowId = wf2.Id;

        var draft1 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Target 1", AutomationWorkflowId = targetWorkflowId };
        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Other 1", AutomationWorkflowId = otherWorkflowId };
        var draft3 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Target 2", AutomationWorkflowId = targetWorkflowId };
        _db.Drafts.AddRange(draft1, draft2, draft3);

        var thread1 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft1.Id, Platform = "MetaThreads", Stage = PlatformThreadStage.Draft };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "MetaThreads", Stage = PlatformThreadStage.Draft };
        var thread3 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft3.Id, Platform = "MetaThreads", Stage = PlatformThreadStage.Draft };
        _db.PlatformThreads.AddRange(thread1, thread2, thread3);

        await _db.SaveChangesAsync();

        var item1 = await _service.EnqueueAsync(schedule.Id, thread1.Id, draft1.Id);
        var item2 = await _service.EnqueueAsync(schedule.Id, thread2.Id, draft2.Id);
        var item3 = await _service.EnqueueAsync(schedule.Id, thread3.Id, draft3.Id);

        var removedCount = await _service.RemoveWorkflowItemsFromQueueAsync(schedule.Id, targetWorkflowId);

        Assert.Equal(2, removedCount);

        var queue = await _service.GetQueueAsync(schedule.Id);
        Assert.Single(queue);
        Assert.Equal(item2.Id, queue[0].Id);
        Assert.Equal(0, queue[0].Position);
        Assert.Equal(otherWorkflowId, queue[0].Draft.AutomationWorkflowId);
    }

    [Fact]
    public async Task EnqueueAsync_WithExplicitPriorityScore_PersistsPriority()
    {
        var (schedule, draft, thread) = await CreateSeedEntitiesAsync();

        var item = await _service.EnqueueAsync(schedule.Id, thread.Id, draft.Id, priorityScore: 9);

        Assert.Equal(9, item.PriorityScore);
        var inDb = await _db.ScheduleQueueItems.FindAsync(item.Id);
        Assert.NotNull(inDb);
        Assert.Equal(9, inDb.PriorityScore);
    }

    [Fact]
    public async Task EnqueueAsync_WithoutPriorityScore_FallsBackToDraftRelevanceScore()
    {
        var (schedule, draft, thread) = await CreateSeedEntitiesAsync();
        draft.RelevanceScore = 8;
        await _db.SaveChangesAsync();

        var item = await _service.EnqueueAsync(schedule.Id, thread.Id, draft.Id);

        Assert.Equal(8, item.PriorityScore);
    }
}

