using System;
using System.Collections.Generic;

namespace SocialWorker.Api.Data.Entities;

public class Schedule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Timezone { get; set; } = "UTC";
    public bool IsActive { get; set; } = true;
    public ICollection<ScheduleSlotRule> SlotRules { get; set; } = new List<ScheduleSlotRule>();
    public ICollection<ScheduleQueueItem> QueueItems { get; set; } = new List<ScheduleQueueItem>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static Schedule Create(Guid userId, string name, string? timezone = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Schedule name is required.", nameof(name));

        var tz = string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone.Trim();

        return new Schedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Timezone = tz,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
