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
}
