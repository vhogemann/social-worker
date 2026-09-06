using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Scheduling.Models;

namespace SocialWorker.Api.Features.Scheduling.Services;

public sealed class ScheduleSlotEvaluatorService
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskQueue _taskQueue;

    public ScheduleSlotEvaluatorService(AppDbContext db, IBackgroundTaskQueue taskQueue)
    {
        _db = db;
        _taskQueue = taskQueue;
    }

    public async Task<int> EvaluateScheduleAsync(
        Guid scheduleId,
        DateTime? referenceTimeUtc = null,
        CancellationToken ct = default)
    {
        var schedule = await _db.Schedules
            .Include(s => s.SlotRules)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.IsActive, ct);

        if (schedule == null || schedule.SlotRules.Count == 0)
            return 0;

        var now = referenceTimeUtc ?? DateTime.UtcNow;
        var toUtc = now.AddHours(48);

        var subSlots = SlotWindowCalculator.CalculateSubSlots(
            schedule.SlotRules,
            SlotWindowCalculator.ResolveTimeZone(schedule.Timezone),
            now,
            toUtc);

        var occupiedSlots = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId && q.AssignedSlotUtc.HasValue && q.Status == ScheduleQueueItemStatus.Dispatched)
            .Select(q => q.AssignedSlotUtc!.Value)
            .ToListAsync(ct);

        var occupiedSet = new HashSet<DateTime>(occupiedSlots);

        var unassignedItems = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId && q.Status == ScheduleQueueItemStatus.Queued && q.AssignedSlotUtc == null)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);

        if (unassignedItems.Count == 0)
            return 0;

        int itemIndex = 0;
        int dispatchedCount = 0;

        foreach (var subSlot in subSlots)
        {
            if (itemIndex >= unassignedItems.Count)
                break;

            if (subSlot.SubSlotUtc <= now || occupiedSet.Contains(subSlot.SubSlotUtc))
                continue;

            var item = unassignedItems[itemIndex++];
            var delay = subSlot.SubSlotUtc > now ? subSlot.SubSlotUtc - now : TimeSpan.Zero;
            var payload = new ScheduledPublishingPayload(item.Id, scheduleId, item.DraftId, item.PlatformThreadId);

            var taskId = await _taskQueue.EnqueueAsync(
                "scheduled-publishing",
                payload,
                new EnqueueTaskOptions
                {
                    Delay = delay,
                    DeduplicationKey = $"publish:{item.Id}"
                },
                ct);

            item.Status = ScheduleQueueItemStatus.Dispatched;
            item.AssignedSlotUtc = subSlot.SubSlotUtc;
            item.ScheduledTaskId = taskId;
            item.UpdatedAt = DateTime.UtcNow;

            occupiedSet.Add(subSlot.SubSlotUtc);
            dispatchedCount++;
        }

        if (dispatchedCount > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return dispatchedCount;
    }

    public async Task<int> EvaluateAllActiveSchedulesAsync(
        DateTime? referenceTimeUtc = null,
        CancellationToken ct = default)
    {
        var scheduleIds = await _db.Schedules
            .Where(s => s.IsActive)
            .Select(s => s.Id)
            .ToListAsync(ct);

        int totalDispatched = 0;
        foreach (var id in scheduleIds)
        {
            totalDispatched += await EvaluateScheduleAsync(id, referenceTimeUtc, ct);
        }

        return totalDispatched;
    }
}
