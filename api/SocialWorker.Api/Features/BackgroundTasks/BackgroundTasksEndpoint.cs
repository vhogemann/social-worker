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
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.BackgroundTasks;

public static class BackgroundTasksEndpoint
{
    public static void MapBackgroundTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/background-tasks")
            .RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            string? taskType,
            BackgroundTaskStatus? status,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.BackgroundTasks.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(taskType))
            {
                query = query.Where(t => t.TaskType == taskType);
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            var total = await query.CountAsync(ct);
            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new BackgroundTaskDto(t))
                .ToListAsync(ct);

            return Results.Ok(new
            {
                items = tasks,
                total,
                page,
                pageSize
            });
        });

        group.MapGet("/stats", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct = default) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var counts = await db.BackgroundTasks
                .AsNoTracking()
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var byType = await db.BackgroundTasks
                .AsNoTracking()
                .GroupBy(t => t.TaskType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

            int pending = counts.FirstOrDefault(c => c.Status == BackgroundTaskStatus.Pending)?.Count ?? 0;
            int running = counts.FirstOrDefault(c => c.Status == BackgroundTaskStatus.Running)?.Count ?? 0;
            int completed = counts.FirstOrDefault(c => c.Status == BackgroundTaskStatus.Completed)?.Count ?? 0;
            int failed = counts.FirstOrDefault(c => c.Status == BackgroundTaskStatus.Failed)?.Count ?? 0;
            int cancelled = counts.FirstOrDefault(c => c.Status == BackgroundTaskStatus.Cancelled)?.Count ?? 0;

            var stats = new BackgroundTaskStatsDto(pending, running, completed, failed, cancelled, byType);
            return Results.Ok(stats);
        });

        group.MapPost("/{id:guid}/retry", async (
            ClaimsPrincipal principal,
            IBackgroundTaskQueue queue,
            Guid id,
            CancellationToken ct = default) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var success = await queue.RetryTaskAsync(id, ct);
            if (!success) return Results.NotFound(new ApiErrorResponse("Task not found."));

            return Results.Ok(new { success = true });
        });

        group.MapPost("/{id:guid}/cancel", async (
            ClaimsPrincipal principal,
            IBackgroundTaskQueue queue,
            Guid id,
            CancellationToken ct = default) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var success = await queue.CancelTaskAsync(id, ct);
            if (!success) return Results.NotFound(new ApiErrorResponse("Task not found."));

            return Results.Ok(new { success = true });
        });

        group.MapDelete("/{id:guid}", async (
            ClaimsPrincipal principal,
            IBackgroundTaskQueue queue,
            Guid id,
            CancellationToken ct = default) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var success = await queue.DeleteTaskAsync(id, ct);
            if (!success) return Results.NotFound(new ApiErrorResponse("Task not found."));

            return Results.NoContent();
        });
    }
}
