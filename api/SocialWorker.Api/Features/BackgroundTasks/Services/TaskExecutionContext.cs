using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;

namespace SocialWorker.Api.Features.BackgroundTasks.Services;

public sealed class TaskExecutionContext : ITaskExecutionContext
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly string _workerId;

    public Guid TaskId { get; }
    public string TaskType { get; }
    public int AttemptCount { get; }
    public int MaxAttempts { get; }
    public CancellationToken CancellationToken { get; }

    public TaskExecutionContext(
        BackgroundTask task,
        string workerId,
        IBackgroundTaskQueue queue,
        CancellationToken ct)
    {
        TaskId = task.Id;
        TaskType = task.TaskType;
        AttemptCount = task.AttemptCount;
        MaxAttempts = task.MaxAttempts;
        _workerId = workerId;
        _queue = queue;
        CancellationToken = ct;
    }

    public Task<bool> ExtendLockAsync(TimeSpan additionalTime, CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, ct);
        return _queue.ExtendLeaseAsync(TaskId, _workerId, additionalTime, linkedCts.Token);
    }
}
