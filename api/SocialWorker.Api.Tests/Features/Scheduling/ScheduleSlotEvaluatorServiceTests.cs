using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class ScheduleSlotEvaluatorServiceTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly TestBackgroundTaskQueue _taskQueue;
    private readonly ScheduleSlotEvaluatorService _evaluator;

    public ScheduleSlotEvaluatorServiceTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
        _taskQueue = new TestBackgroundTaskQueue();
        _evaluator = new ScheduleSlotEvaluatorService(_db, _taskQueue);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    [Fact]
    public async Task EvaluateScheduleAsync_MatchesItemsToUpcomingSlots()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer", "UTC");
        _db.Schedules.Add(schedule);

        // Daily 09:00-10:00 with capacity 2 -> slots at 09:00 and 09:30
        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 2);
        _db.ScheduleSlotRules.Add(rule);

        var draft1 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 1", Content = "Content 1" };
        var thread1 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft1.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var item1 = ScheduleQueueItem.Create(schedule.Id, draft1.Id, thread1.Id, 0);

        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft 2", Content = "Content 2" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var item2 = ScheduleQueueItem.Create(schedule.Id, draft2.Id, thread2.Id, 1);

        _db.Drafts.AddRange(draft1, draft2);
        _db.PlatformThreads.AddRange(thread1, thread2);
        _db.ScheduleQueueItems.AddRange(item1, item2);
        await _db.SaveChangesAsync();

        // Evaluate with reference time at 08:00 UTC on 2026-09-07
        var referenceTime = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc);
        var dispatched = await _evaluator.EvaluateScheduleAsync(schedule.Id, referenceTime);

        Assert.Equal(2, dispatched);
        Assert.Equal(2, _taskQueue.EnqueuedTasks.Count);

        var updatedItem1 = await _db.ScheduleQueueItems.FindAsync(item1.Id);
        var updatedItem2 = await _db.ScheduleQueueItems.FindAsync(item2.Id);

        Assert.NotNull(updatedItem1);
        Assert.Equal(ScheduleQueueItemStatus.Dispatched, updatedItem1.Status);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), updatedItem1.AssignedSlotUtc);
        Assert.NotNull(updatedItem1.ScheduledTaskId);

        Assert.NotNull(updatedItem2);
        Assert.Equal(ScheduleQueueItemStatus.Dispatched, updatedItem2.Status);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 30, 0, DateTimeKind.Utc), updatedItem2.AssignedSlotUtc);
        Assert.NotNull(updatedItem2.ScheduledTaskId);

        // Subsequent evaluation at 08:30 UTC without new items dispatches 0 items
        var secondRun = await _evaluator.EvaluateScheduleAsync(schedule.Id, new DateTime(2026, 9, 7, 8, 30, 0, DateTimeKind.Utc));
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task EvaluateScheduleAsync_TimeDecay_SchedulesFreshLowerScoreBeforeOldHighScore()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer", "UTC");
        _db.Schedules.Add(schedule);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 2);
        _db.ScheduleSlotRules.Add(rule);

        var now = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc);

        // Old item: created 16h ago, base score 9 -> effective score = 9 - (0.25 * 16) = 5.0
        var oldDraft = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Old High Score", CreatedAt = now.AddHours(-16) };
        var oldThread = new PlatformThread { Id = Guid.NewGuid(), DraftId = oldDraft.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var oldItem = ScheduleQueueItem.Create(schedule.Id, oldDraft.Id, oldThread.Id, 0, priorityScore: 9);
        oldItem.CreatedAt = now.AddHours(-16);

        // Fresh item: created 1h ago, base score 8 -> effective score = 8 - (0.25 * 1) = 7.75
        var freshDraft = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Fresh Moderate Score", CreatedAt = now.AddHours(-1) };
        var freshThread = new PlatformThread { Id = Guid.NewGuid(), DraftId = freshDraft.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var freshItem = ScheduleQueueItem.Create(schedule.Id, freshDraft.Id, freshThread.Id, 1, priorityScore: 8);
        freshItem.CreatedAt = now.AddHours(-1);

        _db.Drafts.AddRange(oldDraft, freshDraft);
        _db.PlatformThreads.AddRange(oldThread, freshThread);
        _db.ScheduleQueueItems.AddRange(oldItem, freshItem);
        await _db.SaveChangesAsync();

        var dispatched = await _evaluator.EvaluateScheduleAsync(schedule.Id, now);
        Assert.Equal(2, dispatched);

        var updatedFresh = await _db.ScheduleQueueItems.FindAsync(freshItem.Id);
        var updatedOld = await _db.ScheduleQueueItems.FindAsync(oldItem.Id);

        Assert.NotNull(updatedFresh);
        Assert.NotNull(updatedOld);

        // Fresh item should get 09:00 slot, old item gets 09:30 slot
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), updatedFresh.AssignedSlotUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 30, 0, DateTimeKind.Utc), updatedOld.AssignedSlotUtc);
    }

    [Fact]
    public async Task EvaluateScheduleAsync_Preemption_DisplacesFutureSlotAndCascadesOldItem()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer", "UTC");
        _db.Schedules.Add(schedule);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            "0 14 * * *",
            new TimeOnly(14, 0),
            new TimeOnly(16, 0),
            postCapacity: 2); // 14:00 and 15:00 slots
        _db.ScheduleSlotRules.Add(rule);

        var now = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc); // 14:00 is 6h away, well beyond 1h freeze horizon

        // Incumbent item: base score 6, currently dispatched to 14:00 slot
        var draftA = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Routine Item A", CreatedAt = now.AddHours(-2) };
        var threadA = new PlatformThread { Id = Guid.NewGuid(), DraftId = draftA.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var itemA = ScheduleQueueItem.Create(schedule.Id, draftA.Id, threadA.Id, 0, priorityScore: 6);
        itemA.CreatedAt = now.AddHours(-2);
        itemA.Status = ScheduleQueueItemStatus.Dispatched;
        itemA.AssignedSlotUtc = new DateTime(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc);
        itemA.ScheduledTaskId = Guid.NewGuid();

        // Breaking candidate item: base score 10, fresh (created right now)
        var draftB = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Breaking Scoop B", CreatedAt = now };
        var threadB = new PlatformThread { Id = Guid.NewGuid(), DraftId = draftB.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var itemB = ScheduleQueueItem.Create(schedule.Id, draftB.Id, threadB.Id, 1, priorityScore: 10);
        itemB.CreatedAt = now;

        _db.Drafts.AddRange(draftA, draftB);
        _db.PlatformThreads.AddRange(threadA, threadB);
        _db.ScheduleQueueItems.AddRange(itemA, itemB);
        await _db.SaveChangesAsync();

        var dispatched = await _evaluator.EvaluateScheduleAsync(schedule.Id, now);

        var updatedA = await _db.ScheduleQueueItems.FindAsync(itemA.Id);
        var updatedB = await _db.ScheduleQueueItems.FindAsync(itemB.Id);

        Assert.NotNull(updatedA);
        Assert.NotNull(updatedB);

        // Breaking item B should preempt 14:00 slot
        Assert.Equal(ScheduleQueueItemStatus.Dispatched, updatedB.Status);
        Assert.Equal(new DateTime(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc), updatedB.AssignedSlotUtc);

        // Displaced item A should cascade to 15:00 slot
        Assert.Equal(ScheduleQueueItemStatus.Dispatched, updatedA.Status);
        Assert.Equal(new DateTime(2026, 9, 7, 15, 0, 0, DateTimeKind.Utc), updatedA.AssignedSlotUtc);
    }

    [Fact]
    public async Task EvaluateScheduleAsync_FreezeHorizon_DoesNotPreemptSlotWithinOneHour()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer", "UTC");
        _db.Schedules.Add(schedule);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 2); // 09:00 and 09:30
        _db.ScheduleSlotRules.Add(rule);

        // Reference time: 08:30 UTC -> 09:00 slot is in 30 minutes (inside 1h Freeze Horizon!)
        var now = new DateTime(2026, 9, 7, 8, 30, 0, DateTimeKind.Utc);

        // Incumbent item in 09:00 slot with low score 6
        var draftA = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Routine Item A", CreatedAt = now.AddHours(-1) };
        var threadA = new PlatformThread { Id = Guid.NewGuid(), DraftId = draftA.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var itemA = ScheduleQueueItem.Create(schedule.Id, draftA.Id, threadA.Id, 0, priorityScore: 6);
        itemA.CreatedAt = now.AddHours(-1);
        itemA.Status = ScheduleQueueItemStatus.Dispatched;
        itemA.AssignedSlotUtc = new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc);
        var originalTaskId = Guid.NewGuid();
        itemA.ScheduledTaskId = originalTaskId;

        // Fresh 10/10 breaking item arrives
        var draftB = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Breaking Scoop B", CreatedAt = now };
        var threadB = new PlatformThread { Id = Guid.NewGuid(), DraftId = draftB.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var itemB = ScheduleQueueItem.Create(schedule.Id, draftB.Id, threadB.Id, 1, priorityScore: 10);
        itemB.CreatedAt = now;

        _db.Drafts.AddRange(draftA, draftB);
        _db.PlatformThreads.AddRange(threadA, threadB);
        _db.ScheduleQueueItems.AddRange(itemA, itemB);
        await _db.SaveChangesAsync();

        await _evaluator.EvaluateScheduleAsync(schedule.Id, now);

        var updatedA = await _db.ScheduleQueueItems.FindAsync(itemA.Id);
        var updatedB = await _db.ScheduleQueueItems.FindAsync(itemB.Id);

        Assert.NotNull(updatedA);
        Assert.NotNull(updatedB);

        // Item A remains untouched in 09:00 slot
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), updatedA.AssignedSlotUtc);
        Assert.Equal(originalTaskId, updatedA.ScheduledTaskId);

        // Item B gets the next slot (09:30)
        Assert.Equal(new DateTime(2026, 9, 7, 9, 30, 0, DateTimeKind.Utc), updatedB.AssignedSlotUtc);
    }

    [Fact]
    public async Task EvaluateScheduleAsync_ExpiredItems_MarkedAsExpired()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer", "UTC");
        _db.Schedules.Add(schedule);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 1);
        _db.ScheduleSlotRules.Add(rule);

        var now = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc);

        // Stale item created 75 hours ago
        var staleDraft = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Stale News", CreatedAt = now.AddHours(-75) };
        var staleThread = new PlatformThread { Id = Guid.NewGuid(), DraftId = staleDraft.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var staleItem = ScheduleQueueItem.Create(schedule.Id, staleDraft.Id, staleThread.Id, 0, priorityScore: 7);
        staleItem.CreatedAt = now.AddHours(-75);

        _db.Drafts.Add(staleDraft);
        _db.PlatformThreads.Add(staleThread);
        _db.ScheduleQueueItems.Add(staleItem);
        await _db.SaveChangesAsync();

        var dispatched = await _evaluator.EvaluateScheduleAsync(schedule.Id, now);

        Assert.Equal(0, dispatched);
        var updatedStale = await _db.ScheduleQueueItems.FindAsync(staleItem.Id);
        Assert.NotNull(updatedStale);
        Assert.Equal(ScheduleQueueItemStatus.Expired, updatedStale.Status);
        Assert.Null(updatedStale.AssignedSlotUtc);
    }

    [Fact]
    public async Task EvaluateScheduleAsync_CascadeOverflow_DemotesBackToQueued()
    {
        var schedule = Schedule.Create(_user.Id, "Daily Buffer", "UTC");
        _db.Schedules.Add(schedule);

        // Only 1 slot in the entire 48h horizon: weekly on Monday (day 1) at 14:00 with capacity 1
        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Weekly,
            "0 14 * * 1",
            new TimeOnly(14, 0),
            new TimeOnly(14, 0),
            postCapacity: 1,
            daysOfWeek: new[] { 1 });
        _db.ScheduleSlotRules.Add(rule);

        var now = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc);

        // Incumbent item occupying the single 14:00 slot with low score 6
        var draftA = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Routine Item A", CreatedAt = now.AddHours(-1) };
        var threadA = new PlatformThread { Id = Guid.NewGuid(), DraftId = draftA.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var itemA = ScheduleQueueItem.Create(schedule.Id, draftA.Id, threadA.Id, 0, priorityScore: 6);
        itemA.CreatedAt = now.AddHours(-1);
        itemA.Status = ScheduleQueueItemStatus.Dispatched;
        itemA.AssignedSlotUtc = new DateTime(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc);
        itemA.ScheduledTaskId = Guid.NewGuid();

        // High priority item 10 arrives
        var draftB = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Breaking Scoop B", CreatedAt = now };
        var threadB = new PlatformThread { Id = Guid.NewGuid(), DraftId = draftB.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
        var itemB = ScheduleQueueItem.Create(schedule.Id, draftB.Id, threadB.Id, 1, priorityScore: 10);
        itemB.CreatedAt = now;

        _db.Drafts.AddRange(draftA, draftB);
        _db.PlatformThreads.AddRange(threadA, threadB);
        _db.ScheduleQueueItems.AddRange(itemA, itemB);
        await _db.SaveChangesAsync();

        await _evaluator.EvaluateScheduleAsync(schedule.Id, now);

        var updatedA = await _db.ScheduleQueueItems.FindAsync(itemA.Id);
        var updatedB = await _db.ScheduleQueueItems.FindAsync(itemB.Id);

        Assert.NotNull(updatedA);
        Assert.NotNull(updatedB);

        // Item B occupies the 14:00 slot
        Assert.Equal(ScheduleQueueItemStatus.Dispatched, updatedB.Status);
        Assert.Equal(new DateTime(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc), updatedB.AssignedSlotUtc);

        // Item A was pushed past available slots: demoted back to Queued
        Assert.Equal(ScheduleQueueItemStatus.Queued, updatedA.Status);
        Assert.Null(updatedA.AssignedSlotUtc);
    }
}
