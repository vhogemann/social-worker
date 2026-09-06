using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Scheduling;

public static class SchedulesEndpoint
{
    public static void MapSchedulesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/schedules").RequireAuthorization();

        // 1. List schedules
        group.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var schedules = await db.Schedules
                .Where(s => s.UserId == userId.Value)
                .Include(s => s.SlotRules)
                .Include(s => s.QueueItems)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(ct);

            var dtos = schedules.Select(s => new ScheduleSummaryDto(s)).ToList();
            return Results.Ok(dtos);
        });

        // 2. Create schedule
        group.MapPost("/", async (ClaimsPrincipal principal, AppDbContext db, CreateScheduleRequest request, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Schedule name is required.");

            var schedule = Schedule.Create(userId.Value, request.Name, request.Timezone);
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/schedules/{schedule.Id}", new ScheduleSummaryDto(schedule));
        });

        // 3. Get schedule detail
        group.MapGet("/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var schedule = await db.Schedules
                .Include(s => s.SlotRules)
                .Include(s => s.QueueItems)
                    .ThenInclude(q => q.Draft)
                .Include(s => s.QueueItems)
                    .ThenInclude(q => q.PlatformThread)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);

            if (schedule == null)
                return Results.NotFound();

            return Results.Ok(new ScheduleDetailDto(schedule));
        });

        // 4. Update schedule
        group.MapPut("/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, UpdateScheduleRequest request, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (schedule == null)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Schedule name is required.");

            schedule.Name = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Timezone))
            {
                schedule.Timezone = request.Timezone.Trim();
            }
            schedule.IsActive = request.IsActive;
            schedule.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new ScheduleSummaryDto(schedule));
        });

        // 5. Delete schedule
        group.MapDelete("/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (schedule == null)
                return Results.NotFound();

            db.Schedules.Remove(schedule);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // 6. List slot rules
        group.MapGet("/{id:guid}/slots", async (ClaimsPrincipal principal, AppDbContext db, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            var slots = await db.ScheduleSlotRules
                .Where(r => r.ScheduleId == id)
                .OrderBy(r => r.StartTime)
                .ToListAsync(ct);

            return Results.Ok(slots.Select(s => new ScheduleSlotRuleDto(s)).ToList());
        });

        // 7. Create slot rule
        group.MapPost("/{id:guid}/slots", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            ScheduleSlotEvaluatorService evaluator,
            Guid id,
            CreateSlotRuleRequest request,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (schedule == null) return Results.NotFound();

            string cron = request.CronExpression?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cron))
            {
                cron = request.RecurrenceType switch
                {
                    SlotRecurrenceType.Daily => CronExpressionBuilder.Daily(request.StartTime),
                    SlotRecurrenceType.Weekly => CronExpressionBuilder.Weekly(request.StartTime, request.DaysOfWeek ?? new[] { 1, 2, 3, 4, 5 }),
                    SlotRecurrenceType.Monthly => CronExpressionBuilder.Monthly(request.StartTime, request.DayOfMonth ?? 1),
                    SlotRecurrenceType.Yearly => CronExpressionBuilder.Yearly(request.StartTime, request.MonthOfYear ?? 1, request.DayOfMonth ?? 1),
                    _ => throw new ArgumentException("Cron expression is required for custom recurrence type.")
                };
            }

            if (!CronExpressionBuilder.TryValidate(cron, out var cronError))
            {
                return Results.BadRequest($"Invalid cron expression: {cronError}");
            }

            if (request.EndTime < request.StartTime)
            {
                return Results.BadRequest("End time cannot be earlier than start time.");
            }

            if (request.PostCapacity < 1)
            {
                return Results.BadRequest("Post capacity must be at least 1.");
            }

            var rule = ScheduleSlotRule.Create(
                id,
                request.RecurrenceType,
                cron,
                request.StartTime,
                request.EndTime,
                request.PostCapacity,
                request.DaysOfWeek,
                request.DayOfMonth,
                request.MonthOfYear);

            db.ScheduleSlotRules.Add(rule);
            await db.SaveChangesAsync(ct);

            // Re-evaluate schedule immediately to populate newly created slot
            await evaluator.EvaluateScheduleAsync(id, ct: ct);

            return Results.Created($"/api/schedules/{id}/slots/{rule.Id}", new ScheduleSlotRuleDto(rule));
        });

        // 8. Delete slot rule
        group.MapDelete("/{id:guid}/slots/{slotId:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, Guid slotId, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var rule = await db.ScheduleSlotRules
                .Include(r => r.Schedule)
                .FirstOrDefaultAsync(r => r.Id == slotId && r.ScheduleId == id && r.Schedule.UserId == userId.Value, ct);

            if (rule == null) return Results.NotFound();

            db.ScheduleSlotRules.Remove(rule);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // 9. Get queue items
        group.MapGet("/{id:guid}/queue", async (ClaimsPrincipal principal, AppDbContext db, IScheduleQueueService queueService, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            var queue = await queueService.GetQueueAsync(id, ct);
            return Results.Ok(queue.Select(q => new ScheduleQueueItemDto(q)).ToList());
        });

        // 10. Enqueue item
        group.MapPost("/{id:guid}/queue", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IScheduleQueueService queueService,
            ScheduleSlotEvaluatorService evaluator,
            Guid id,
            EnqueueItemRequest request,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            try
            {
                var item = await queueService.EnqueueAsync(id, request.PlatformThreadId, request.DraftId, request.TargetPosition, ct);
                await evaluator.EvaluateScheduleAsync(id, ct: ct);
                return Results.Created($"/api/schedules/{id}/queue/{item.Id}", new ScheduleQueueItemDto(item));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // 11. Reorder queue
        group.MapPut("/{id:guid}/queue/reorder", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IScheduleQueueService queueService,
            ScheduleSlotEvaluatorService evaluator,
            Guid id,
            ReorderQueueRequest request,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            var reordered = await queueService.ReorderQueueAsync(id, request.ItemIdsInOrder, ct);
            await evaluator.EvaluateScheduleAsync(id, ct: ct);
            return Results.Ok(reordered.Select(q => new ScheduleQueueItemDto(q)).ToList());
        });

        // 12. Remove queue item
        group.MapDelete("/{id:guid}/queue/{itemId:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IScheduleQueueService queueService,
            Guid id,
            Guid itemId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            var removed = await queueService.RemoveFromQueueAsync(id, itemId, ct);
            if (!removed) return Results.NotFound();

            return Results.NoContent();
        });

        // 13. Pause / unpause queue item
        group.MapPost("/{id:guid}/queue/{itemId:guid}/pause", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IScheduleQueueService queueService,
            ScheduleSlotEvaluatorService evaluator,
            Guid id,
            Guid itemId,
            PauseQueueItemRequest request,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            var item = await queueService.PauseQueueItemAsync(id, itemId, request.IsPaused, ct);
            if (item == null) return Results.NotFound();

            if (!request.IsPaused)
            {
                await evaluator.EvaluateScheduleAsync(id, ct: ct);
            }

            return Results.Ok(new ScheduleQueueItemDto(item));
        });

        // 14. Calendar projection feed
        app.MapGet("/api/calendar", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            DateTime? start,
            DateTime? end,
            Guid? scheduleId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var fromUtc = start ?? DateTime.UtcNow.Date;
            var toUtc = end ?? fromUtc.AddDays(30);

            if (toUtc <= fromUtc)
                return Results.BadRequest("End date must be greater than start date.");

            // 1. Fetch relevant user schedules
            var schedulesQuery = db.Schedules
                .Where(s => s.UserId == userId.Value && s.IsActive)
                .Include(s => s.SlotRules)
                .AsQueryable();

            if (scheduleId.HasValue)
            {
                schedulesQuery = schedulesQuery.Where(s => s.Id == scheduleId.Value);
            }

            var schedules = await schedulesQuery.ToListAsync(ct);
            var events = new List<CalendarEventDto>();

            // 2. Fetch assigned queue items in range
            var queueItemsQuery = db.ScheduleQueueItems
                .Where(q => q.Schedule.UserId == userId.Value && q.AssignedSlotUtc.HasValue && q.AssignedSlotUtc >= fromUtc && q.AssignedSlotUtc < toUtc)
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
            var posts = await db.Posts
                .Where(p => p.Draft.UserId == userId.Value && p.PostedAt >= fromUtc && p.PostedAt < toUtc)
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

            return Results.Ok(events.OrderBy(e => e.TimestampUtc).ToList());
        }).RequireAuthorization();
    }
}
