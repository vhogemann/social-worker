using System;

namespace SocialWorker.Api.Data.Entities;

public class ScheduleQueueItem
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;
    public Guid PlatformThreadId { get; set; }
    public PlatformThread PlatformThread { get; set; } = null!;
    public int Position { get; set; }
    public int? PriorityScore { get; set; }
    public ScheduleQueueItemStatus Status { get; set; } = ScheduleQueueItemStatus.Queued;
    public DateTime? AssignedSlotUtc { get; set; }
    public Guid? ScheduledTaskId { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static ScheduleQueueItem Create(
        Guid scheduleId,
        Guid draftId,
        Guid platformThreadId,
        int position,
        int? priorityScore = null)
    {
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("Schedule ID is required.", nameof(scheduleId));
        if (draftId == Guid.Empty)
            throw new ArgumentException("Draft ID is required.", nameof(draftId));
        if (platformThreadId == Guid.Empty)
            throw new ArgumentException("Platform thread ID is required.", nameof(platformThreadId));
        if (position < 0)
            throw new ArgumentException("Position cannot be negative.", nameof(position));

        return new ScheduleQueueItem
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            DraftId = draftId,
            PlatformThreadId = platformThreadId,
            Position = position,
            PriorityScore = priorityScore.HasValue ? Math.Clamp(priorityScore.Value, 1, 10) : null,
            Status = ScheduleQueueItemStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
