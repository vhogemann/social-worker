using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.BackgroundTasks.Services;

public sealed class PostgresBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostgresBackgroundTaskQueue> _logger;
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public PostgresBackgroundTaskQueue(
        IServiceScopeFactory scopeFactory,
        ILogger<PostgresBackgroundTaskQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<Guid> EnqueueAsync<T>(
        string taskType,
        T payload,
        EnqueueTaskOptions? options = null,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var opts = options ?? EnqueueTaskOptions.Default;

        if (!string.IsNullOrWhiteSpace(opts.DeduplicationKey))
        {
            var existing = await db.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.DeduplicationKey == opts.DeduplicationKey &&
                            (t.Status == BackgroundTaskStatus.Pending || t.Status == BackgroundTaskStatus.Running))
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(ct);

            if (existing.HasValue)
            {
                _logger.LogDebug("Task with deduplication key {Key} already exists ({TaskId})", opts.DeduplicationKey, existing.Value);
                return existing.Value;
            }
        }

        var task = BackgroundTask.Create(taskType, payload, opts);
        db.BackgroundTasks.Add(task);
        await db.SaveChangesAsync(ct);

        _signal.Writer.TryWrite(true);
        _logger.LogDebug("Enqueued background task {TaskId} of type {TaskType}", task.Id, task.TaskType);
        return task.Id;
    }

    public async Task<BackgroundTask?> DequeueNextAsync(
        string workerId,
        TimeSpan defaultLease,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        if (db.Database.IsNpgsql())
        {
            var leaseSeconds = (int)Math.Max(1, defaultLease.TotalSeconds);
            var sql = @"
UPDATE ""BackgroundTasks""
SET ""Status"" = 1,
    ""LockedUntil"" = NOW() + (@p0 || ' seconds')::interval,
    ""LockedByWorker"" = @p1,
    ""AttemptCount"" = ""AttemptCount"" + 1,
    ""UpdatedAt"" = NOW()
WHERE ""Id"" = (
    SELECT ""Id""
    FROM ""BackgroundTasks""
    WHERE (""Status"" = 0 OR (""Status"" = 1 AND ""LockedUntil"" < NOW()))
      AND ""NextAttemptAt"" <= NOW()
    ORDER BY ""Priority"" DESC, ""NextAttemptAt"" ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
RETURNING *;";

            var lockedTask = await db.BackgroundTasks
                .FromSqlRaw(sql, leaseSeconds.ToString(), workerId)
                .AsTracking()
                .FirstOrDefaultAsync(ct);

            return lockedTask;
        }
        else
        {
            var candidate = await db.BackgroundTasks
                .Where(t => (t.Status == BackgroundTaskStatus.Pending || (t.Status == BackgroundTaskStatus.Running && t.LockedUntil < now)) &&
                            t.NextAttemptAt <= now)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.NextAttemptAt)
                .FirstOrDefaultAsync(ct);

            if (candidate == null)
            {
                return null;
            }

            candidate.MarkRunning(workerId, defaultLease);
            await db.SaveChangesAsync(ct);
            return candidate;
        }
    }

    public async Task MarkCompletedAsync(Guid taskId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null)
        {
            task.MarkCompleted();
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkRetryAsync(Guid taskId, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null)
        {
            task.MarkRetry(error, retryDelay);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(Guid taskId, string error, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null)
        {
            task.MarkFailed(error);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExtendLeaseAsync(Guid taskId, string workerId, TimeSpan extension, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.Status == BackgroundTaskStatus.Running && task.LockedByWorker == workerId)
        {
            task.LockedUntil = (task.LockedUntil ?? DateTime.UtcNow).Add(extension);
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }
        return false;
    }

    public async Task<bool> CancelTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.Status != BackgroundTaskStatus.Completed)
        {
            task.MarkCancelled();
            await db.SaveChangesAsync(ct);
            return true;
        }
        return false;
    }

    public async Task<bool> RetryTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null)
        {
            task.Status = BackgroundTaskStatus.Pending;
            task.AttemptCount = 0;
            task.NextAttemptAt = DateTime.UtcNow;
            task.LastError = null;
            task.LockedByWorker = null;
            task.LockedUntil = null;
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _signal.Writer.TryWrite(true);
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.BackgroundTasks.FindAsync(new object[] { taskId }, ct);
        if (task != null)
        {
            db.BackgroundTasks.Remove(task);
            await db.SaveChangesAsync(ct);
            return true;
        }
        return false;
    }

    public async IAsyncEnumerable<bool> WaitForWorkSignalAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var hasSignal = await _signal.Reader.WaitToReadAsync(ct);
            if (hasSignal && _signal.Reader.TryRead(out _))
            {
                yield return true;
            }
        }
    }
}
