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

        var expiredItems = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId && q.Status == ScheduleQueueItemStatus.Queued && q.AssignedSlotUtc == null)
            .ToListAsync(ct);

        foreach (var item in expiredItems)
        {
            if (SchedulePriorityCalculator.IsExpired(item.CreatedAt, now))
            {
                item.Status = ScheduleQueueItemStatus.Expired;
                item.UpdatedAt = now;
            }
        }

        var subSlots = SlotWindowCalculator.CalculateSubSlots(
            schedule.SlotRules,
            SlotWindowCalculator.ResolveTimeZone(schedule.Timezone),
            now,
            toUtc);

        var futureSlots = subSlots
            .Where(s => s.SubSlotUtc > now)
            .OrderBy(s => s.SubSlotUtc)
            .ToList();

        if (futureSlots.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
            return 0;
        }

        var allActiveItems = await _db.ScheduleQueueItems
            .Include(q => q.Draft)
            .Where(q => q.ScheduleId == scheduleId && (q.Status == ScheduleQueueItemStatus.Queued || q.Status == ScheduleQueueItemStatus.Dispatched))
            .ToListAsync(ct);

        var slotOccupants = new Dictionary<DateTime, ScheduleQueueItem>();
        foreach (var item in allActiveItems.Where(i => i.Status == ScheduleQueueItemStatus.Dispatched && i.AssignedSlotUtc.HasValue))
        {
            slotOccupants[item.AssignedSlotUtc!.Value] = item;
        }

        var candidatePool = allActiveItems
            .Where(i => i.Status == ScheduleQueueItemStatus.Queued && i.AssignedSlotUtc == null)
            .ToList();

        int dispatchedCount = 0;

        foreach (var slot in futureSlots)
        {
            var isFrozen = SchedulePriorityCalculator.IsFrozen(slot.SubSlotUtc, now);
            slotOccupants.TryGetValue(slot.SubSlotUtc, out var incumbent);

            if (isFrozen)
            {
                if (incumbent == null && candidatePool.Count > 0)
                {
                    var bestCandidate = PopBestCandidate(candidatePool, now);
                    await AssignSlotAsync(bestCandidate, scheduleId, slot.SubSlotUtc, now, ct);
                    slotOccupants[slot.SubSlotUtc] = bestCandidate;
                    dispatchedCount++;
                }
                continue;
            }

            if (incumbent == null)
            {
                if (candidatePool.Count > 0)
                {
                    var bestCandidate = PopBestCandidate(candidatePool, now);
                    await AssignSlotAsync(bestCandidate, scheduleId, slot.SubSlotUtc, now, ct);
                    slotOccupants[slot.SubSlotUtc] = bestCandidate;
                    dispatchedCount++;
                }
            }
            else
            {
                if (candidatePool.Count > 0)
                {
                    var bestCandidate = PeekBestCandidate(candidatePool, now);
                    var candidateEffective = SchedulePriorityCalculator.CalculateEffectiveScore(bestCandidate.PriorityScore ?? bestCandidate.Draft?.RelevanceScore, bestCandidate.CreatedAt, now);
                    var incumbentEffective = SchedulePriorityCalculator.CalculateEffectiveScore(incumbent.PriorityScore ?? incumbent.Draft?.RelevanceScore, incumbent.CreatedAt, now);

                    if (SchedulePriorityCalculator.CanPreempt(candidateEffective, incumbentEffective))
                    {
                        candidatePool.Remove(bestCandidate);

                        if (incumbent.ScheduledTaskId.HasValue)
                        {
                            await _taskQueue.CancelTaskAsync(incumbent.ScheduledTaskId.Value, ct);
                        }

                        incumbent.Status = ScheduleQueueItemStatus.Queued;
                        incumbent.AssignedSlotUtc = null;
                        incumbent.ScheduledTaskId = null;
                        incumbent.UpdatedAt = now;
                        candidatePool.Add(incumbent);

                        await AssignSlotAsync(bestCandidate, scheduleId, slot.SubSlotUtc, now, ct);
                        slotOccupants[slot.SubSlotUtc] = bestCandidate;
                        dispatchedCount++;
                    }
                }
            }
        }

        var remainingQueued = candidatePool
            .OrderByDescending(c => SchedulePriorityCalculator.CalculateEffectiveScore(c.PriorityScore ?? c.Draft?.RelevanceScore, c.CreatedAt, now))
            .ThenBy(c => c.CreatedAt)
            .ToList();

        for (int i = 0; i < remainingQueued.Count; i++)
        {
            var item = remainingQueued[i];
            item.Position = i;
            item.AssignedSlotUtc = null;
            item.Status = ScheduleQueueItemStatus.Queued;
            item.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return dispatchedCount;
    }

    private static ScheduleQueueItem PopBestCandidate(List<ScheduleQueueItem> pool, DateTime now)
    {
        var best = PeekBestCandidate(pool, now);
        pool.Remove(best);
        return best;
    }

    private static ScheduleQueueItem PeekBestCandidate(List<ScheduleQueueItem> pool, DateTime now)
    {
        return pool
            .OrderByDescending(c => SchedulePriorityCalculator.CalculateEffectiveScore(c.PriorityScore ?? c.Draft?.RelevanceScore, c.CreatedAt, now))
            .ThenBy(c => c.CreatedAt)
            .ThenBy(c => c.Position)
            .First();
    }

    private async Task AssignSlotAsync(ScheduleQueueItem item, Guid scheduleId, DateTime slotUtc, DateTime now, CancellationToken ct)
    {
        var delay = slotUtc > now ? slotUtc - now : TimeSpan.Zero;
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
        item.AssignedSlotUtc = slotUtc;
        item.ScheduledTaskId = taskId;
        item.UpdatedAt = now;
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
