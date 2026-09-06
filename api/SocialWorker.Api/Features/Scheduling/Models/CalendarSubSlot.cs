using System;

namespace SocialWorker.Api.Features.Scheduling.Models;

public sealed record CalendarSubSlot(
    Guid ScheduleId,
    Guid SlotRuleId,
    DateTime SubSlotUtc,
    int SubSlotIndex,
    int TotalCapacity
);
