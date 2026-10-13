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
                return Results.BadRequest(new ApiErrorResponse("Schedule name is required."));

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
                return Results.NotFound(new ApiErrorResponse("Schedule not found."));

            return Results.Ok(new ScheduleDetailDto(schedule));
        });

        // 4. Update schedule
        group.MapPut("/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, UpdateScheduleRequest request, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (schedule == null)
                return Results.NotFound(new ApiErrorResponse("Schedule not found."));

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new ApiErrorResponse("Schedule name is required."));

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
                return Results.BadRequest(new ApiErrorResponse($"Invalid cron expression: {cronError}"));
            }

            if (request.EndTime < request.StartTime)
            {
                return Results.BadRequest(new ApiErrorResponse("End time cannot be earlier than start time."));
            }

            if (request.PostCapacity < 1)
            {
                return Results.BadRequest(new ApiErrorResponse("Post capacity must be at least 1."));
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

            if (rule == null) return Results.NotFound(new ApiErrorResponse("Slot rule not found."));

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
            if (!exists) return Results.NotFound(new ApiErrorResponse("Schedule not found."));

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
            if (!exists) return Results.NotFound(new ApiErrorResponse("Schedule not found."));

            Guid resolvedThreadId;
            if (request.PlatformThreadId.HasValue && request.PlatformThreadId.Value != Guid.Empty)
            {
                resolvedThreadId = request.PlatformThreadId.Value;
            }
            else
            {
                var draft = await db.Drafts
                    .Include(d => d.Threads)
                    .FirstOrDefaultAsync(d => d.Id == request.DraftId && d.UserId == userId.Value, ct);
                if (draft == null)
                {
                    return Results.BadRequest(new ApiErrorResponse($"Draft with ID '{request.DraftId}' was not found."));
                }
                var thread = draft.Threads.FirstOrDefault();
                if (thread == null)
                {
                    return Results.BadRequest(new ApiErrorResponse($"No platform thread found for draft '{request.DraftId}'."));
                }
                resolvedThreadId = thread.Id;
            }

            try
            {
                var item = await queueService.EnqueueAsync(id, resolvedThreadId, request.DraftId, request.TargetPosition, request.PriorityScore, ct);
                await evaluator.EvaluateScheduleAsync(id, ct: ct);
                return Results.Created($"/api/schedules/{id}/queue/{item.Id}", new ScheduleQueueItemDto(item));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ApiErrorResponse(ex.Message));
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

        // 12b. Remove queue items for workflow
        group.MapDelete("/{id:guid}/queue", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            IScheduleQueueService queueService,
            Guid id,
            Guid? workflowId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var exists = await db.Schedules.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
            if (!exists) return Results.NotFound();

            if (!workflowId.HasValue)
            {
                return Results.BadRequest(new ApiErrorResponse("workflowId query parameter is required."));
            }

            var removedCount = await queueService.RemoveWorkflowItemsFromQueueAsync(id, workflowId.Value, ct);
            return Results.Ok(new { removedCount });
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
            ICalendarProjectionService calendarService,
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
                return Results.BadRequest(new ApiErrorResponse("End date must be greater than start date."));

            var events = await calendarService.GetCalendarEventsAsync(userId.Value, fromUtc, toUtc, scheduleId, ct);
            return Results.Ok(events);
        }).RequireAuthorization();
    }
}
