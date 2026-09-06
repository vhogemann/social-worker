using System;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class CronExpressionBuilderTests
{
    [Fact]
    public void Daily_ReturnsExpectedCron()
    {
        var cron = CronExpressionBuilder.Daily(new TimeOnly(9, 30));
        Assert.Equal("30 9 * * *", cron);
        Assert.True(CronExpressionBuilder.TryValidate(cron, out var error), error);
    }

    [Fact]
    public void Weekly_SortsAndDeduplicatesDays()
    {
        var cron = CronExpressionBuilder.Weekly(new TimeOnly(14, 0), new[] { 5, 1, 3, 1 });
        Assert.Equal("0 14 * * 1,3,5", cron);
        Assert.True(CronExpressionBuilder.TryValidate(cron, out var error), error);
    }

    [Fact]
    public void Weekly_EmptyOrInvalidDays_Throws()
    {
        Assert.Throws<ArgumentException>(() => CronExpressionBuilder.Weekly(new TimeOnly(9, 0), Array.Empty<int>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpressionBuilder.Weekly(new TimeOnly(9, 0), new[] { 7 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpressionBuilder.Weekly(new TimeOnly(9, 0), new[] { -1 }));
    }

    [Fact]
    public void Monthly_ReturnsExpectedCron()
    {
        var cron = CronExpressionBuilder.Monthly(new TimeOnly(8, 15), 15);
        Assert.Equal("15 8 15 * *", cron);
        Assert.True(CronExpressionBuilder.TryValidate(cron, out var error), error);
    }

    [Fact]
    public void Monthly_InvalidDay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpressionBuilder.Monthly(new TimeOnly(8, 0), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpressionBuilder.Monthly(new TimeOnly(8, 0), 32));
    }

    [Fact]
    public void Yearly_ReturnsExpectedCron()
    {
        var cron = CronExpressionBuilder.Yearly(new TimeOnly(10, 0), 12, 25);
        Assert.Equal("0 10 25 12 *", cron);
        Assert.True(CronExpressionBuilder.TryValidate(cron, out var error), error);
    }

    [Fact]
    public void Yearly_InvalidMonthOrDay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpressionBuilder.Yearly(new TimeOnly(10, 0), 13, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CronExpressionBuilder.Yearly(new TimeOnly(10, 0), 1, 32));
    }

    [Theory]
    [InlineData("0 9 * * *", true)]
    [InlineData("*/15 * * * *", true)]
    [InlineData("invalid cron expression", false)]
    [InlineData("", false)]
    public void TryValidate_IdentifiesValidAndInvalidCron(string cron, bool expectedValid)
    {
        var isValid = CronExpressionBuilder.TryValidate(cron, out var error);
        Assert.Equal(expectedValid, isValid);
        if (expectedValid)
            Assert.Null(error);
        else
            Assert.NotNull(error);
    }
}
