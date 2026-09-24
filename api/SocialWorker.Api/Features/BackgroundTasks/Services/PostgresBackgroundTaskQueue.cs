using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
        options ??= EnqueueTaskOptions.Default;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!string.IsNullOrWhiteSpace(options.DeduplicationKey))
        {
            var existing = await db.BackgroundTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.DeduplicationKey == options.DeduplicationKey &&
                    (t.Status == BackgroundTaskStatus.Pending || t.Status == BackgroundTaskStatus.Running), ct);

            if (existing != null)
            {
                _logger.LogDebug("Task of type '{TaskType}' with key '{Key}' already active (id: {TaskId}), skipping",
                    taskType, options.DeduplicationKey, existing.Id);
                return existing.Id;
            }
        }

        var task = BackgroundTask.Create(taskType, payload, options);
        db.BackgroundTasks.Add(task);
        await db.SaveChangesAsync(ct);

        _signal.Writer.TryWrite(true);
        _logger.LogInformation("Enqueued background task {TaskId} of type '{TaskType}' (priority: {Priority})",
            task.Id, taskType, task.Priority);

        return task.Id;
    }

    public async Task<BackgroundTask?> DequeueNextAsync(
        string workerId,
        TimeSpan defaultLease,
        IReadOnlyCollection<string>? allowedTaskTypes = null,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var allowedList = allowedTaskTypes?.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        var hasFilter = allowedList != null && allowedList.Length > 0 && !allowedList.Contains("*");

        if (db.Database.IsNpgsql())
        {
            var leaseSeconds = (int)Math.Max(1, defaultLease.TotalSeconds);
            var filterClause = hasFilter ? " AND \"TaskType\" = ANY(@p2)" : "";
            var sql = $@"
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
      AND ""NextAttemptAt"" <= NOW(){filterClause}
    ORDER BY ""Priority"" DESC, ""NextAttemptAt"" ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
RETURNING ""Id"", ""TaskType"", ""PayloadJson"", ""Status"", ""Priority"", ""AttemptCount"", ""MaxAttempts"", ""NextAttemptAt"", ""LockedUntil"", ""LockedByWorker"", ""LastError"", ""DeduplicationKey"", ""CreatedAt"", ""UpdatedAt"", ""CompletedAt"";";

            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var p0 = command.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = leaseSeconds.ToString();
            command.Parameters.Add(p0);

            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = workerId;
            command.Parameters.Add(p1);

            if (hasFilter)
            {
                var p2 = command.CreateParameter();
                p2.ParameterName = "@p2";
                p2.Value = allowedList!;
                command.Parameters.Add(p2);
            }

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return MapTaskFromReader(reader);
            }

            return null;
        }
        else
        {
            var query = db.BackgroundTasks
                .Where(t => (t.Status == BackgroundTaskStatus.Pending || (t.Status == BackgroundTaskStatus.Running && t.LockedUntil < now)) &&
                            t.NextAttemptAt <= now);

            if (hasFilter)
            {
                query = query.Where(t => allowedList!.Contains(t.TaskType));
            }

            var candidate = await query
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

    private static BackgroundTask MapTaskFromReader(DbDataReader reader)
    {
        return new BackgroundTask
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            TaskType = reader.GetString(reader.GetOrdinal("TaskType")),
            PayloadJson = reader.GetString(reader.GetOrdinal("PayloadJson")),
            Status = (BackgroundTaskStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            Priority = reader.GetInt32(reader.GetOrdinal("Priority")),
            AttemptCount = reader.GetInt32(reader.GetOrdinal("AttemptCount")),
            MaxAttempts = reader.GetInt32(reader.GetOrdinal("MaxAttempts")),
            NextAttemptAt = reader.GetDateTime(reader.GetOrdinal("NextAttemptAt")),
            LockedUntil = reader.IsDBNull(reader.GetOrdinal("LockedUntil")) ? null : reader.GetDateTime(reader.GetOrdinal("LockedUntil")),
            LockedByWorker = reader.IsDBNull(reader.GetOrdinal("LockedByWorker")) ? null : reader.GetString(reader.GetOrdinal("LockedByWorker")),
            LastError = reader.IsDBNull(reader.GetOrdinal("LastError")) ? null : reader.GetString(reader.GetOrdinal("LastError")),
            DeduplicationKey = reader.IsDBNull(reader.GetOrdinal("DeduplicationKey")) ? null : reader.GetString(reader.GetOrdinal("DeduplicationKey")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedAt"))
        };
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
