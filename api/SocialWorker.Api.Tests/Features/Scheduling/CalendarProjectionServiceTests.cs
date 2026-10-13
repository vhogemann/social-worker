using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class CalendarProjectionServiceTests : SqliteTestBase
{
    [Fact]
    public async Task GetCalendarEventsAsync_WhenMultipleQueueItemsShareSameAssignedSlot_DoesNotThrow()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var schedule = Schedule.Create(user.Id, "Daily Schedule", "UTC");
        db.Schedules.Add(schedule);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            "0 12 * * *",
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            1);
        db.ScheduleSlotRules.Add(rule);

        var slotTime = DateTime.UtcNow.Date.AddHours(12);

        var draft1 = Draft.Create(user.Id, "Draft 1");
        var draft2 = Draft.Create(user.Id, "Draft 2");
        db.Drafts.AddRange(draft1, draft2);

        var thread1 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft1.Id, Platform = "Bluesky", Content = "Draft 1" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Content = "Draft 2" };
        db.PlatformThreads.AddRange(thread1, thread2);

        var item1 = ScheduleQueueItem.Create(schedule.Id, draft1.Id, thread1.Id, 1);
        item1.AssignedSlotUtc = slotTime;
        item1.Status = ScheduleQueueItemStatus.Succeeded;

        var item2 = ScheduleQueueItem.Create(schedule.Id, draft2.Id, thread2.Id, 2);
        item2.AssignedSlotUtc = slotTime;
        item2.Status = ScheduleQueueItemStatus.Dispatched;

        db.ScheduleQueueItems.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var service = new CalendarProjectionService(db);
        var fromUtc = DateTime.UtcNow.Date;
        var toUtc = fromUtc.AddDays(1);

        var events = await service.GetCalendarEventsAsync(user.Id, fromUtc, toUtc, schedule.Id, CancellationToken.None);

        Assert.NotNull(events);
    }
}
