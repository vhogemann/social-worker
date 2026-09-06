using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class ScheduleEntityTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public ScheduleEntityTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    [Fact]
    public void ScheduleCreate_ValidParameters_ReturnsInitializedSchedule()
    {
        var schedule = Schedule.Create(_user.Id, "  Main Buffer  ", "America/New_York");

        Assert.NotEqual(Guid.Empty, schedule.Id);
        Assert.Equal(_user.Id, schedule.UserId);
        Assert.Equal("Main Buffer", schedule.Name);
        Assert.Equal("America/New_York", schedule.Timezone);
        Assert.True(schedule.IsActive);
        Assert.True(schedule.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void ScheduleCreate_DefaultTimezone_FallsBackToUtc()
    {
        var schedule = Schedule.Create(_user.Id, "Evening Slot", null);

        Assert.Equal("UTC", schedule.Timezone);
    }

    [Fact]
    public void ScheduleCreate_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Schedule.Create(Guid.Empty, "Valid Name"));
        Assert.Throws<ArgumentException>(() => Schedule.Create(_user.Id, "   "));
        Assert.Throws<ArgumentException>(() => Schedule.Create(_user.Id, ""));
    }

    [Fact]
    public void ScheduleSlotRuleCreate_ValidParameters_ReturnsInitializedRule()
    {
        var scheduleId = Guid.NewGuid();
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(10, 0);
        var days = new[] { 1, 2, 3, 4, 5 };

        var rule = ScheduleSlotRule.Create(
            scheduleId,
            SlotRecurrenceType.Weekly,
            "0 9 * * 1-5",
            start,
            end,
            postCapacity: 4,
            daysOfWeek: days);

        Assert.NotEqual(Guid.Empty, rule.Id);
        Assert.Equal(scheduleId, rule.ScheduleId);
        Assert.Equal(SlotRecurrenceType.Weekly, rule.RecurrenceType);
        Assert.Equal("0 9 * * 1-5", rule.CronExpression);
        Assert.Equal(start, rule.StartTime);
        Assert.Equal(end, rule.EndTime);
        Assert.Equal(4, rule.PostCapacity);
        Assert.Equal(days, rule.DaysOfWeek);
    }

    [Fact]
    public void ScheduleSlotRuleCreate_InvalidParameters_ThrowsArgumentException()
    {
        var scheduleId = Guid.NewGuid();
        var start = new TimeOnly(10, 0);
        var end = new TimeOnly(9, 0);

        Assert.Throws<ArgumentException>(() => ScheduleSlotRule.Create(Guid.Empty, SlotRecurrenceType.Daily, "* * * * *", start, end));
        Assert.Throws<ArgumentException>(() => ScheduleSlotRule.Create(scheduleId, SlotRecurrenceType.Daily, "   ", start, end));
        Assert.Throws<ArgumentException>(() => ScheduleSlotRule.Create(scheduleId, SlotRecurrenceType.Daily, "* * * * *", start, end, postCapacity: 0));
        Assert.Throws<ArgumentException>(() => ScheduleSlotRule.Create(scheduleId, SlotRecurrenceType.Daily, "* * * * *", start, end, postCapacity: 1)); // End < Start
    }

    [Fact]
    public void ScheduleQueueItemCreate_ValidParameters_ReturnsInitializedItem()
    {
        var scheduleId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var threadId = Guid.NewGuid();

        var item = ScheduleQueueItem.Create(scheduleId, draftId, threadId, 0);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(scheduleId, item.ScheduleId);
        Assert.Equal(draftId, item.DraftId);
        Assert.Equal(threadId, item.PlatformThreadId);
        Assert.Equal(0, item.Position);
        Assert.Equal(ScheduleQueueItemStatus.Queued, item.Status);
        Assert.Null(item.AssignedSlotUtc);
    }

    [Fact]
    public void ScheduleQueueItemCreate_InvalidParameters_ThrowsArgumentException()
    {
        var validId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => ScheduleQueueItem.Create(Guid.Empty, validId, validId, 0));
        Assert.Throws<ArgumentException>(() => ScheduleQueueItem.Create(validId, Guid.Empty, validId, 0));
        Assert.Throws<ArgumentException>(() => ScheduleQueueItem.Create(validId, validId, Guid.Empty, 0));
        Assert.Throws<ArgumentException>(() => ScheduleQueueItem.Create(validId, validId, validId, -1));
    }

    [Fact]
    public void AutomationWorkflowDestinationCreate_PublishingMode_InitializesProperties()
    {
        var workflowId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        var destScheduled = AutomationWorkflowDestination.Create(
            workflowId,
            "Bluesky",
            WorkflowPublishingMode.Scheduled,
            scheduleId);

        Assert.Equal(WorkflowPublishingMode.Scheduled, destScheduled.PublishingMode);
        Assert.Equal(scheduleId, destScheduled.ScheduleId);
        Assert.False(destScheduled.AutoPublish);

        var destAuto = AutomationWorkflowDestination.Create(
            workflowId,
            "Bluesky",
            WorkflowPublishingMode.Automatic);

        Assert.Equal(WorkflowPublishingMode.Automatic, destAuto.PublishingMode);
        Assert.True(destAuto.AutoPublish);
    }

    [Fact]
    public async Task Schedule_DatabasePersistenceAndCascadeDelete()
    {
        var schedule = Schedule.Create(_user.Id, "Primary Queue", "UTC");
        _db.Schedules.Add(schedule);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Weekly,
            "0 9 * * 1,3,5",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 2,
            daysOfWeek: new[] { 1, 3, 5 });
        _db.ScheduleSlotRules.Add(rule);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Draft to Schedule",
            Content = "Thread segment"
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Scheduled
        };
        _db.PlatformThreads.Add(thread);

        var queueItem = ScheduleQueueItem.Create(schedule.Id, draft.Id, thread.Id, 0);
        _db.ScheduleQueueItems.Add(queueItem);

        await _db.SaveChangesAsync();

        // Verify retrieval and value conversion of DaysOfWeek
        var savedRule = await _db.ScheduleSlotRules.FindAsync(rule.Id);
        Assert.NotNull(savedRule);
        Assert.NotNull(savedRule.DaysOfWeek);
        Assert.Equal(new[] { 1, 3, 5 }, savedRule.DaysOfWeek);

        // Verify cascade delete from Schedule to SlotRule and QueueItem
        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.ScheduleSlotRules.Where(r => r.ScheduleId == schedule.Id).ToListAsync());
        Assert.Empty(await _db.ScheduleQueueItems.Where(q => q.ScheduleId == schedule.Id).ToListAsync());
    }
}
