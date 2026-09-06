using System;
using System.Linq;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class SlotWindowCalculatorTests
{
    [Fact]
    public void ProjectSubSlots_EqualIntervalPacing_DividesWindowEvenly()
    {
        var scheduleId = Guid.NewGuid();
        var rule = ScheduleSlotRule.Create(
            scheduleId,
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 4);

        var fromUtc = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 9, 7, 23, 59, 59, DateTimeKind.Utc);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(rule, TimeZoneInfo.Utc, fromUtc, toUtc);

        Assert.Equal(4, subSlots.Count);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), subSlots[0].SubSlotUtc);
        Assert.Equal(0, subSlots[0].SubSlotIndex);

        Assert.Equal(new DateTime(2026, 9, 7, 9, 15, 0, DateTimeKind.Utc), subSlots[1].SubSlotUtc);
        Assert.Equal(1, subSlots[1].SubSlotIndex);

        Assert.Equal(new DateTime(2026, 9, 7, 9, 30, 0, DateTimeKind.Utc), subSlots[2].SubSlotUtc);
        Assert.Equal(2, subSlots[2].SubSlotIndex);

        Assert.Equal(new DateTime(2026, 9, 7, 9, 45, 0, DateTimeKind.Utc), subSlots[3].SubSlotUtc);
        Assert.Equal(3, subSlots[3].SubSlotIndex);

        Assert.All(subSlots, s => Assert.Equal(4, s.TotalCapacity));
        Assert.All(subSlots, s => Assert.Equal(scheduleId, s.ScheduleId));
    }

    [Fact]
    public void ProjectSubSlots_SinglePost_YieldsSingleSubSlotAtStart()
    {
        var rule = ScheduleSlotRule.Create(
            Guid.NewGuid(),
            SlotRecurrenceType.Daily,
            "30 8 * * *",
            new TimeOnly(8, 30),
            new TimeOnly(9, 0),
            postCapacity: 1);

        var fromUtc = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 9, 7, 23, 59, 59, DateTimeKind.Utc);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(rule, TimeZoneInfo.Utc, fromUtc, toUtc);

        var single = Assert.Single(subSlots);
        Assert.Equal(new DateTime(2026, 9, 7, 8, 30, 0, DateTimeKind.Utc), single.SubSlotUtc);
        Assert.Equal(0, single.SubSlotIndex);
        Assert.Equal(1, single.TotalCapacity);
    }

    [Fact]
    public void ProjectSubSlots_WeeklyRecurrence_OnlyEmitsConfiguredDays()
    {
        // Monday (1), Wednesday (3), Friday (5) at 10:00
        var rule = ScheduleSlotRule.Create(
            Guid.NewGuid(),
            SlotRecurrenceType.Weekly,
            "0 10 * * 1,3,5",
            new TimeOnly(10, 0),
            new TimeOnly(10, 30),
            postCapacity: 1,
            daysOfWeek: new[] { 1, 3, 5 });

        // Week of Sep 7, 2026 (Mon) to Sep 13, 2026 (Sun)
        var fromUtc = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(rule, TimeZoneInfo.Utc, fromUtc, toUtc);

        Assert.Equal(3, subSlots.Count);
        Assert.Equal(DayOfWeek.Monday, subSlots[0].SubSlotUtc.DayOfWeek);
        Assert.Equal(DayOfWeek.Wednesday, subSlots[1].SubSlotUtc.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, subSlots[2].SubSlotUtc.DayOfWeek);
    }

    [Fact]
    public void ProjectSubSlots_TimeWindowBoundary_ClipsOutsideSubSlots()
    {
        // 09:00 to 10:00 with capacity 4 -> subslots at 09:00, 09:15, 09:30, 09:45
        var rule = ScheduleSlotRule.Create(
            Guid.NewGuid(),
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            postCapacity: 4);

        // Filter from 09:16 to 09:40 -> only 09:30 falls in range
        var fromUtc = new DateTime(2026, 9, 7, 9, 16, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 9, 7, 9, 40, 0, DateTimeKind.Utc);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(rule, TimeZoneInfo.Utc, fromUtc, toUtc);

        var single = Assert.Single(subSlots);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 30, 0, DateTimeKind.Utc), single.SubSlotUtc);
        Assert.Equal(2, single.SubSlotIndex);
    }

    [Fact]
    public void ProjectSubSlots_DST_SpringForward_HandlesLocalTimeShift()
    {
        // Europe/London springs forward on Sunday, March 29, 2026
        // Before transition: GMT = UTC+0. 09:00 local is 09:00 UTC.
        // After transition: BST = UTC+1. 09:00 local is 08:00 UTC.
        var tz = SlotWindowCalculator.ResolveTimeZone("Europe/London");

        var rule = ScheduleSlotRule.Create(
            Guid.NewGuid(),
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(9, 30),
            postCapacity: 1);

        var fromUtc = new DateTime(2026, 3, 28, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(rule, tz, fromUtc, toUtc);

        Assert.Equal(3, subSlots.Count);
        // March 28: GMT (UTC+0) -> 09:00 UTC
        Assert.Equal(new DateTime(2026, 3, 28, 9, 0, 0, DateTimeKind.Utc), subSlots[0].SubSlotUtc);
        // March 29: BST (UTC+1) -> 08:00 UTC
        Assert.Equal(new DateTime(2026, 3, 29, 8, 0, 0, DateTimeKind.Utc), subSlots[1].SubSlotUtc);
        // March 30: BST (UTC+1) -> 08:00 UTC
        Assert.Equal(new DateTime(2026, 3, 30, 8, 0, 0, DateTimeKind.Utc), subSlots[2].SubSlotUtc);
    }

    [Fact]
    public void ProjectSubSlots_DST_FallBack_HandlesLocalTimeShift()
    {
        // Europe/London falls back on Sunday, October 25, 2026
        // Before transition: BST = UTC+1. 09:00 local is 08:00 UTC.
        // After transition: GMT = UTC+0. 09:00 local is 09:00 UTC.
        var tz = SlotWindowCalculator.ResolveTimeZone("Europe/London");

        var rule = ScheduleSlotRule.Create(
            Guid.NewGuid(),
            SlotRecurrenceType.Daily,
            "0 9 * * *",
            new TimeOnly(9, 0),
            new TimeOnly(9, 30),
            postCapacity: 1);

        var fromUtc = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(rule, tz, fromUtc, toUtc);

        Assert.Equal(3, subSlots.Count);
        // Oct 24: BST (UTC+1) -> 08:00 UTC
        Assert.Equal(new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc), subSlots[0].SubSlotUtc);
        // Oct 25: GMT (UTC+0) -> 09:00 UTC
        Assert.Equal(new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), subSlots[1].SubSlotUtc);
        // Oct 26: GMT (UTC+0) -> 09:00 UTC
        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc), subSlots[2].SubSlotUtc);
    }
}
