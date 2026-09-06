using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Scheduling.Models;

namespace SocialWorker.Api.Features.Scheduling.Services;

public interface ICalendarProjectionService
{
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        Guid? scheduleId = null,
        CancellationToken ct = default);
}

public sealed class CalendarProjectionService : ICalendarProjectionService
{
    private readonly AppDbContext _db;

    public CalendarProjectionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        Guid? scheduleId = null,
        CancellationToken ct = default)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("End date must be greater than start date.", nameof(toUtc));
        }

        // 1. Fetch relevant user schedules
        var schedulesQuery = _db.Schedules
            .Where(s => s.UserId == userId && s.IsActive)
            .Include(s => s.SlotRules)
            .AsQueryable();

        if (scheduleId.HasValue)
        {
            schedulesQuery = schedulesQuery.Where(s => s.Id == scheduleId.Value);
        }

        var schedules = await schedulesQuery.ToListAsync(ct);
        var events = new List<CalendarEventDto>();

        // 2. Fetch assigned queue items in range
        var queueItemsQuery = _db.ScheduleQueueItems
            .Where(q => q.Schedule.UserId == userId && q.AssignedSlotUtc.HasValue && q.AssignedSlotUtc >= fromUtc && q.AssignedSlotUtc < toUtc)
            .Include(q => q.Schedule)
            .Include(q => q.Draft)
            .Include(q => q.PlatformThread)
            .AsQueryable();

        if (scheduleId.HasValue)
        {
            queueItemsQuery = queueItemsQuery.Where(q => q.ScheduleId == scheduleId.Value);
        }

        var assignedQueueItems = await queueItemsQuery.ToListAsync(ct);
        var assignedMap = assignedQueueItems
            .Where(q => q.AssignedSlotUtc.HasValue)
            .ToDictionary(q => (q.ScheduleId, q.AssignedSlotUtc!.Value));

        // 3. Project slot occurrences and mark filled vs open
        foreach (var schedule in schedules)
        {
            var tz = SlotWindowCalculator.ResolveTimeZone(schedule.Timezone);
            var subSlots = SlotWindowCalculator.CalculateSubSlots(schedule.SlotRules, tz, fromUtc, toUtc);

            foreach (var slot in subSlots)
            {
                if (assignedMap.TryGetValue((schedule.Id, slot.SubSlotUtc), out var item))
                {
                    events.Add(new CalendarEventDto(
                        Id: item.Id.ToString(),
                        Type: CalendarEventType.ScheduledPost,
                        TimestampUtc: slot.SubSlotUtc,
                        ScheduleId: schedule.Id,
                        ScheduleName: schedule.Name,
                        DraftId: item.DraftId,
                        Title: item.Draft?.Title ?? "Scheduled Post",
                        PlatformThreadId: item.PlatformThreadId,
                        Platform: item.PlatformThread?.Platform,
                        RemoteUrl: null,
                        SubSlotIndex: slot.SubSlotIndex,
                        TotalCapacity: slot.TotalCapacity
                    ));
                }
                else
                {
                    events.Add(new CalendarEventDto(
                        Id: $"slot_{slot.SlotRuleId}_{slot.SubSlotUtc:yyyyMMddHHmmss}_{slot.SubSlotIndex}",
                        Type: CalendarEventType.OpenSlot,
                        TimestampUtc: slot.SubSlotUtc,
                        ScheduleId: schedule.Id,
                        ScheduleName: schedule.Name,
                        DraftId: null,
                        Title: null,
                        PlatformThreadId: null,
                        Platform: null,
                        RemoteUrl: null,
                        SubSlotIndex: slot.SubSlotIndex,
                        TotalCapacity: slot.TotalCapacity
                    ));
                }
            }
        }

        // 4. Fetch published posts in range
        var posts = await _db.Posts
            .Where(p => p.Draft.UserId == userId && p.PostedAt >= fromUtc && p.PostedAt < toUtc)
            .Include(p => p.Draft)
            .Include(p => p.PlatformThread)
            .ToListAsync(ct);

        foreach (var post in posts)
        {
            events.Add(new CalendarEventDto(
                Id: post.Id.ToString(),
                Type: CalendarEventType.PublishedPost,
                TimestampUtc: post.PostedAt,
                ScheduleId: null,
                ScheduleName: null,
                DraftId: post.DraftId,
                Title: post.Draft?.Title ?? "Published Post",
                PlatformThreadId: post.PlatformThreadId,
                Platform: post.Platform,
                RemoteUrl: post.Url,
                SubSlotIndex: post.SegmentIndex,
                TotalCapacity: null
            ));
        }

        return events.OrderBy(e => e.TimestampUtc).ToList();
    }
}
