using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;

namespace SocialWorker.Api.Features.Scheduling.Services;

public sealed class ScheduleQueueService : IScheduleQueueService
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskQueue _taskQueue;

    public ScheduleQueueService(AppDbContext db, IBackgroundTaskQueue taskQueue)
    {
        _db = db;
        _taskQueue = taskQueue;
    }

    public async Task<ScheduleQueueItem> EnqueueAsync(
        Guid scheduleId,
        Guid platformThreadId,
        Guid draftId,
        int? targetPosition = null,
        CancellationToken ct = default)
    {
        var scheduleExists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId, ct);
        if (!scheduleExists)
            throw new InvalidOperationException($"Schedule with ID '{scheduleId}' was not found.");

        var thread = await _db.PlatformThreads.FirstOrDefaultAsync(t => t.Id == platformThreadId, ct);
        if (thread == null)
            throw new InvalidOperationException($"Platform thread with ID '{platformThreadId}' was not found.");

        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == draftId, ct);
        if (!draftExists)
            throw new InvalidOperationException($"Draft with ID '{draftId}' was not found.");

        var existingItems = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);

        int newPosition;
        if (targetPosition.HasValue)
        {
            newPosition = Math.Clamp(targetPosition.Value, 0, existingItems.Count);
            foreach (var item in existingItems.Where(i => i.Position >= newPosition))
            {
                item.Position++;
            }
        }
        else
        {
            newPosition = existingItems.Count > 0 ? existingItems.Max(i => i.Position) + 1 : 0;
        }

        var queueItem = ScheduleQueueItem.Create(scheduleId, draftId, platformThreadId, newPosition);
        thread.Stage = PlatformThreadStage.Scheduled;
        thread.UpdatedAt = DateTime.UtcNow;

        _db.ScheduleQueueItems.Add(queueItem);
        await _db.SaveChangesAsync(ct);

        return queueItem;
    }

    public async Task<IReadOnlyList<ScheduleQueueItem>> ReorderQueueAsync(
        Guid scheduleId,
        IReadOnlyList<Guid> itemIdsInOrder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(itemIdsInOrder);

        var items = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId)
            .ToListAsync(ct);

        var itemMap = items.ToDictionary(i => i.Id);
        int currentPos = 0;

        foreach (var id in itemIdsInOrder)
        {
            if (itemMap.TryGetValue(id, out var item))
            {
                item.Position = currentPos++;
                itemMap.Remove(id);
            }
        }

        foreach (var remaining in itemMap.Values.OrderBy(i => i.Position))
        {
            remaining.Position = currentPos++;
        }

        await _db.SaveChangesAsync(ct);

        return items.OrderBy(i => i.Position).ToList();
    }

    public async Task<bool> RemoveFromQueueAsync(Guid scheduleId, Guid queueItemId, CancellationToken ct = default)
    {
        var item = await _db.ScheduleQueueItems
            .FirstOrDefaultAsync(q => q.Id == queueItemId && q.ScheduleId == scheduleId, ct);

        if (item == null)
            return false;

        if (item.ScheduledTaskId.HasValue)
        {
            await _taskQueue.CancelTaskAsync(item.ScheduledTaskId.Value, ct);
        }

        var thread = await _db.PlatformThreads.FirstOrDefaultAsync(t => t.Id == item.PlatformThreadId, ct);
        if (thread != null && thread.Stage == PlatformThreadStage.Scheduled)
        {
            thread.Stage = PlatformThreadStage.Draft;
            thread.UpdatedAt = DateTime.UtcNow;
        }

        _db.ScheduleQueueItems.Remove(item);

        var remaining = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId && q.Id != queueItemId)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);

        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].Position = i;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RemoveWorkflowItemsFromQueueAsync(Guid scheduleId, Guid workflowId, CancellationToken ct = default)
    {
        var items = await _db.ScheduleQueueItems
            .Include(q => q.Draft)
            .Where(q => q.ScheduleId == scheduleId && q.Draft.AutomationWorkflowId == workflowId)
            .ToListAsync(ct);

        if (items.Count == 0)
            return 0;

        var threadIds = items.Select(i => i.PlatformThreadId).Distinct().ToList();
        var threads = await _db.PlatformThreads
            .Where(t => threadIds.Contains(t.Id))
            .ToListAsync(ct);
        var threadMap = threads.ToDictionary(t => t.Id);

        foreach (var item in items)
        {
            if (item.ScheduledTaskId.HasValue)
            {
                await _taskQueue.CancelTaskAsync(item.ScheduledTaskId.Value, ct);
            }

            if (threadMap.TryGetValue(item.PlatformThreadId, out var thread) && thread.Stage == PlatformThreadStage.Scheduled)
            {
                thread.Stage = PlatformThreadStage.Draft;
                thread.UpdatedAt = DateTime.UtcNow;
            }

            _db.ScheduleQueueItems.Remove(item);
        }

        await _db.SaveChangesAsync(ct);

        var remaining = await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);

        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].Position = i;
        }

        await _db.SaveChangesAsync(ct);
        return items.Count;
    }

    public async Task<ScheduleQueueItem?> PauseQueueItemAsync(
        Guid scheduleId,
        Guid queueItemId,
        bool isPaused,
        CancellationToken ct = default)
    {
        var item = await _db.ScheduleQueueItems
            .FirstOrDefaultAsync(q => q.Id == queueItemId && q.ScheduleId == scheduleId, ct);

        if (item == null)
            return null;

        if (isPaused)
        {
            item.Status = ScheduleQueueItemStatus.Paused;
            if (item.ScheduledTaskId.HasValue)
            {
                await _taskQueue.CancelTaskAsync(item.ScheduledTaskId.Value, ct);
                item.ScheduledTaskId = null;
                item.AssignedSlotUtc = null;
            }
        }
        else
        {
            item.Status = ScheduleQueueItemStatus.Queued;
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return item;
    }

    public async Task<IReadOnlyList<ScheduleQueueItem>> GetQueueAsync(Guid scheduleId, CancellationToken ct = default)
    {
        return await _db.ScheduleQueueItems
            .Where(q => q.ScheduleId == scheduleId)
            .Include(q => q.Draft)
            .Include(q => q.PlatformThread)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);
    }
}
