using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Tests;

public class TestBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly List<BackgroundTask> _tasks = new();

    public IReadOnlyList<BackgroundTask> EnqueuedTasks => _tasks.AsReadOnly();

    public Task<Guid> EnqueueAsync<T>(
        string taskType,
        T payload,
        EnqueueTaskOptions? options = null,
        CancellationToken ct = default)
    {
        var task = BackgroundTask.Create(taskType, payload, options);
        _tasks.Add(task);
        return Task.FromResult(task.Id);
    }

    public Task<BackgroundTask?> DequeueNextAsync(
        string workerId,
        TimeSpan defaultLease,
        IReadOnlyCollection<string>? allowedTaskTypes = null,
        CancellationToken ct = default)
    {
        var allowedList = allowedTaskTypes?.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        var hasFilter = allowedList != null && allowedList.Length > 0 && !allowedList.Contains("*");

        var task = _tasks.FirstOrDefault(t =>
            t.Status == BackgroundTaskStatus.Pending &&
            (!hasFilter || allowedList!.Contains(t.TaskType)));

        if (task != null)
        {
            task.MarkRunning(workerId, defaultLease);
        }
        return Task.FromResult(task);
    }

    public Task MarkCompletedAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        task?.MarkCompleted();
        return Task.CompletedTask;
    }

    public Task MarkRetryAsync(Guid taskId, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        task?.MarkRetry(error, retryDelay);
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid taskId, string error, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        task?.MarkFailed(error);
        return Task.CompletedTask;
    }

    public Task<bool> ExtendLeaseAsync(Guid taskId, string workerId, TimeSpan extension, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId && t.Status == BackgroundTaskStatus.Running);
        if (task != null)
        {
            task.LockedUntil = (task.LockedUntil ?? DateTime.UtcNow).Add(extension);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> CancelTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.MarkCancelled();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> RetryTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.Status = BackgroundTaskStatus.Pending;
            task.AttemptCount = 0;
            task.NextAttemptAt = DateTime.UtcNow;
            task.LastError = null;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            _tasks.Remove(task);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async IAsyncEnumerable<bool> WaitForWorkSignalAsync([EnumeratorCancellation] CancellationToken ct)
    {
        yield return true;
        await Task.CompletedTask;
    }
}
