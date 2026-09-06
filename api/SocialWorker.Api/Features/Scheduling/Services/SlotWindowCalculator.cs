using System;
using System.Collections.Generic;
using System.Linq;
using Cronos;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Models;

namespace SocialWorker.Api.Features.Scheduling.Services;

public static class SlotWindowCalculator
{
    public static TimeZoneInfo ResolveTimeZone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId.Trim());
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static IReadOnlyList<CalendarSubSlot> CalculateSubSlots(
        ScheduleSlotRule rule,
        string? timezoneId,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var tz = ResolveTimeZone(timezoneId);
        return CalculateSubSlots(rule, tz, fromUtc, toUtc);
    }

    public static IReadOnlyList<CalendarSubSlot> CalculateSubSlots(
        ScheduleSlotRule rule,
        TimeZoneInfo timezone,
        DateTime fromUtc,
        DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timezone);

        if (fromUtc >= toUtc)
            return Array.Empty<CalendarSubSlot>();

        var normalizedFromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        var normalizedToUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);

        var cron = CronExpression.Parse(rule.CronExpression, CronFormat.Standard);
        var windowDuration = rule.EndTime >= rule.StartTime
            ? rule.EndTime - rule.StartTime
            : TimeSpan.Zero;

        var capacity = Math.Max(1, rule.PostCapacity);
        var interval = capacity > 1 && windowDuration > TimeSpan.Zero
            ? windowDuration / capacity
            : TimeSpan.Zero;

        var searchFromUtc = normalizedFromUtc > DateTime.MinValue + windowDuration
            ? normalizedFromUtc - windowDuration
            : normalizedFromUtc;

        var occurrences = cron.GetOccurrences(
            searchFromUtc,
            normalizedToUtc,
            timezone,
            fromInclusive: true,
            toInclusive: false);

        var results = new List<CalendarSubSlot>();

        foreach (var windowStartUtc in occurrences)
        {
            for (var k = 0; k < capacity; k++)
            {
                var subSlotUtc = windowStartUtc + (k * interval);
                if (subSlotUtc >= normalizedFromUtc && subSlotUtc < normalizedToUtc)
                {
                    results.Add(new CalendarSubSlot(
                        rule.ScheduleId,
                        rule.Id,
                        subSlotUtc,
                        k,
                        capacity
                    ));
                }
            }
        }

        return results.OrderBy(s => s.SubSlotUtc).ThenBy(s => s.SubSlotIndex).ToList();
    }

    public static IReadOnlyList<CalendarSubSlot> CalculateSubSlots(
        IEnumerable<ScheduleSlotRule> rules,
        TimeZoneInfo timezone,
        DateTime fromUtc,
        DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(timezone);

        var results = new List<CalendarSubSlot>();
        foreach (var rule in rules)
        {
            results.AddRange(CalculateSubSlots(rule, timezone, fromUtc, toUtc));
        }

        return results.OrderBy(s => s.SubSlotUtc).ThenBy(s => s.SubSlotIndex).ToList();
    }
}
