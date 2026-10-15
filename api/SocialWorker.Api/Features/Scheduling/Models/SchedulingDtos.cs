using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Services;

namespace SocialWorker.Api.Features.Scheduling.Models;

public sealed record ScheduleSummaryDto(
    Guid Id,
    string Name,
    string Timezone,
    bool IsActive,
    int SlotRuleCount,
    int QueueItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public ScheduleSummaryDto(Schedule s)
        : this(
            s.Id,
            s.Name,
            s.Timezone,
            s.IsActive,
            s.SlotRules?.Count ?? 0,
            s.QueueItems?.Count(q => q.Status == ScheduleQueueItemStatus.Queued || q.Status == ScheduleQueueItemStatus.Dispatched || q.Status == ScheduleQueueItemStatus.Paused) ?? 0,
            s.CreatedAt,
            s.UpdatedAt)
    {
    }
}

public sealed record ScheduleSlotRuleDto(
    Guid Id,
    Guid ScheduleId,
    SlotRecurrenceType RecurrenceType,
    string CronExpression,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int PostCapacity,
    int[]? DaysOfWeek,
    int? DayOfMonth,
    int? MonthOfYear,
    DateTime CreatedAt
)
{
    public ScheduleSlotRuleDto(ScheduleSlotRule r)
        : this(
            r.Id,
            r.ScheduleId,
            r.RecurrenceType,
            r.CronExpression,
            r.StartTime,
            r.EndTime,
            r.PostCapacity,
            r.DaysOfWeek,
            r.DayOfMonth,
            r.MonthOfYear,
            r.CreatedAt)
    {
    }
}

public sealed record ScheduleQueueItemDto(
    Guid Id,
    Guid ScheduleId,
    Guid DraftId,
    string DraftTitle,
    Guid PlatformThreadId,
    string Platform,
    int Position,
    int? PriorityScore,
    double? EffectivePriorityScore,
    ScheduleQueueItemStatus Status,
    DateTime? AssignedSlotUtc,
    string? LastError,
    DateTime CreatedAt
)
{
    public ScheduleQueueItemDto(ScheduleQueueItem q, DateTime? nowUtc = null)
        : this(
            q.Id,
            q.ScheduleId,
            q.DraftId,
            q.Draft?.Title ?? "Untitled Draft",
            q.PlatformThreadId,
            q.PlatformThread?.Platform ?? "Unknown",
            q.Position,
            q.PriorityScore ?? q.Draft?.RelevanceScore ?? 7,
            SchedulePriorityCalculator.CalculateEffectiveScore(q.PriorityScore ?? q.Draft?.RelevanceScore, q.CreatedAt, nowUtc ?? DateTime.UtcNow),
            q.Status,
            q.AssignedSlotUtc,
            q.LastError,
            q.CreatedAt)
    {
    }
}

public sealed record ScheduleDetailDto(
    Guid Id,
    string Name,
    string Timezone,
    bool IsActive,
    List<ScheduleSlotRuleDto> SlotRules,
    List<ScheduleQueueItemDto> QueueItems,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public ScheduleDetailDto(Schedule s)
        : this(
            s.Id,
            s.Name,
            s.Timezone,
            s.IsActive,
            s.SlotRules?.Select(r => new ScheduleSlotRuleDto(r)).ToList() ?? new(),
            s.QueueItems?
                .Where(q => q.Status == ScheduleQueueItemStatus.Queued || q.Status == ScheduleQueueItemStatus.Dispatched || q.Status == ScheduleQueueItemStatus.Paused)
                .OrderBy(q => q.Position)
                .Select(q => new ScheduleQueueItemDto(q))
                .ToList() ?? new(),
            s.CreatedAt,
            s.UpdatedAt)
    {
    }
}

public sealed record CreateScheduleRequest(string Name, string? Timezone = "UTC");

public sealed record UpdateScheduleRequest(string Name, string? Timezone, bool IsActive);

public sealed record CreateSlotRuleRequest(
    SlotRecurrenceType RecurrenceType,
    string? CronExpression,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int PostCapacity = 1,
    int[]? DaysOfWeek = null,
    int? DayOfMonth = null,
    int? MonthOfYear = null
);

public sealed record EnqueueItemRequest(
    Guid DraftId,
    Guid? PlatformThreadId = null,
    int? TargetPosition = null,
    int? PriorityScore = null
);

public sealed record ReorderQueueRequest(List<Guid> ItemIdsInOrder);

public sealed record PauseQueueItemRequest(bool IsPaused);

public enum CalendarEventType
{
    OpenSlot,
    ScheduledPost,
    PublishedPost
}

public sealed record CalendarEventDto(
    string Id,
    CalendarEventType Type,
    DateTime TimestampUtc,
    Guid? ScheduleId,
    string? ScheduleName,
    Guid? DraftId,
    string? Title,
    Guid? PlatformThreadId,
    string? Platform,
    string? RemoteUrl,
    int? SubSlotIndex,
    int? TotalCapacity
);

public sealed record OutboxDraftDto(
    Guid DraftId,
    string Title,
    string Content,
    Guid? WorkflowId,
    string? WorkflowTitle,
    List<OutboxThreadDto> Threads,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record OutboxThreadDto(
    Guid ThreadId,
    string Platform,
    PlatformThreadStage Stage,
    string? Content,
    Guid? AccountId,
    string? AccountHandle
);

public sealed record ApproveOutboxRequest(
    string Action,
    Guid? ScheduleId = null,
    Guid? PlatformThreadId = null
);
