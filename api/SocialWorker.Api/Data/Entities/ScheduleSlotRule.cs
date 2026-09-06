using System;

namespace SocialWorker.Api.Data.Entities;

public class ScheduleSlotRule
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;
    public SlotRecurrenceType RecurrenceType { get; set; } = SlotRecurrenceType.Daily;
    public string CronExpression { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int PostCapacity { get; set; } = 1;
    public int[]? DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public int? MonthOfYear { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static ScheduleSlotRule Create(
        Guid scheduleId,
        SlotRecurrenceType recurrenceType,
        string cronExpression,
        TimeOnly startTime,
        TimeOnly endTime,
        int postCapacity = 1,
        int[]? daysOfWeek = null,
        int? dayOfMonth = null,
        int? monthOfYear = null)
    {
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("Schedule ID is required.", nameof(scheduleId));
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression is required.", nameof(cronExpression));
        if (postCapacity < 1)
            throw new ArgumentException("Post capacity must be at least 1.", nameof(postCapacity));
        if (endTime < startTime)
            throw new ArgumentException("End time cannot be earlier than start time.", nameof(endTime));

        return new ScheduleSlotRule
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            RecurrenceType = recurrenceType,
            CronExpression = cronExpression.Trim(),
            StartTime = startTime,
            EndTime = endTime,
            PostCapacity = postCapacity,
            DaysOfWeek = daysOfWeek,
            DayOfMonth = dayOfMonth,
            MonthOfYear = monthOfYear,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
