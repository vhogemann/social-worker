using System;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class SchedulePriorityCalculatorTests
{
    [Fact]
    public void CalculateEffectiveScore_FreshItem_ReturnsBaseScore()
    {
        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        var score = SchedulePriorityCalculator.CalculateEffectiveScore(10, now, now);
        Assert.Equal(10.0, score);
    }

    [Fact]
    public void CalculateEffectiveScore_DefaultScoreWhenNull_UsesSeven()
    {
        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        var score = SchedulePriorityCalculator.CalculateEffectiveScore(null, now, now);
        Assert.Equal(7.0, score);
    }

    [Fact]
    public void CalculateEffectiveScore_FourHoursOld_DecaysByOnePoint()
    {
        var now = new DateTime(2026, 10, 13, 16, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var score = SchedulePriorityCalculator.CalculateEffectiveScore(10, created, now);
        Assert.Equal(9.0, score);
    }

    [Fact]
    public void CalculateEffectiveScore_TwelveHoursOld_DecaysByThreePoints()
    {
        var now = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var score = SchedulePriorityCalculator.CalculateEffectiveScore(10, created, now);
        Assert.Equal(7.0, score);
    }

    [Fact]
    public void CalculateEffectiveScore_VeryOldItem_RespectsFloorOfOne()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var score = SchedulePriorityCalculator.CalculateEffectiveScore(10, created, now);
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void IsExpired_Under72Hours_ReturnsFalse()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var created = now.AddHours(-71.5);
        Assert.False(SchedulePriorityCalculator.IsExpired(created, now));
    }

    [Fact]
    public void IsExpired_Over72Hours_ReturnsTrue()
    {
        var now = new DateTime(2026, 10, 16, 13, 0, 0, DateTimeKind.Utc);
        var created = now.AddHours(-72.1);
        Assert.True(SchedulePriorityCalculator.IsExpired(created, now));
    }

    [Fact]
    public void CanPreempt_ExceedsIncumbentByAtLeastThreshold_ReturnsTrue()
    {
        Assert.True(SchedulePriorityCalculator.CanPreempt(9.5, 8.0));
        Assert.True(SchedulePriorityCalculator.CanPreempt(10.0, 7.0));
    }

    [Fact]
    public void CanPreempt_BelowThreshold_ReturnsFalse()
    {
        Assert.False(SchedulePriorityCalculator.CanPreempt(8.5, 8.0));
        Assert.False(SchedulePriorityCalculator.CanPreempt(9.0, 8.0));
    }

    [Fact]
    public void IsFrozen_WithinOneHour_ReturnsTrue()
    {
        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        var slotSoon = now.AddMinutes(45);
        Assert.True(SchedulePriorityCalculator.IsFrozen(slotSoon, now));
    }

    [Fact]
    public void IsFrozen_BeyondOneHour_ReturnsFalse()
    {
        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        var slotLater = now.AddHours(2);
        Assert.False(SchedulePriorityCalculator.IsFrozen(slotLater, now));
    }
}
