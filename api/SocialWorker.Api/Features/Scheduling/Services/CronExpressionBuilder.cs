using System;
using System.Collections.Generic;
using System.Linq;
using Cronos;

namespace SocialWorker.Api.Features.Scheduling.Services;

public static class CronExpressionBuilder
{
    public static string Daily(TimeOnly start) => $"{start.Minute} {start.Hour} * * *";

    public static string Weekly(TimeOnly start, IEnumerable<int> daysOfWeek)
    {
        var days = daysOfWeek?.Distinct().OrderBy(d => d).ToList();
        if (days == null || days.Count == 0)
            throw new ArgumentException("At least one day of the week must be specified.", nameof(daysOfWeek));

        if (days.Any(d => d < 0 || d > 6))
            throw new ArgumentOutOfRangeException(nameof(daysOfWeek), "Days of week must be between 0 (Sunday) and 6 (Saturday).");

        return $"{start.Minute} {start.Hour} * * {string.Join(",", days)}";
    }

    public static string Monthly(TimeOnly start, int dayOfMonth)
    {
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31.");

        return $"{start.Minute} {start.Hour} {dayOfMonth} * *";
    }

    public static string Yearly(TimeOnly start, int monthOfYear, int dayOfMonth)
    {
        if (monthOfYear < 1 || monthOfYear > 12)
            throw new ArgumentOutOfRangeException(nameof(monthOfYear), "Month of year must be between 1 and 12.");
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31.");

        return $"{start.Minute} {start.Hour} {dayOfMonth} {monthOfYear} *";
    }

    public static bool TryValidate(string cronExpression, out string? error)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            error = "Cron expression cannot be empty.";
            return false;
        }

        try
        {
            Cronos.CronExpression.Parse(cronExpression.Trim(), CronFormat.Standard);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
