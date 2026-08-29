using System;
using System.Text.Json;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Data.Entities;

public class BackgroundTask
{
    public Guid Id { get; set; }
    public string TaskType { get; set; } = null!;
    public string PayloadJson { get; set; } = "{}";
    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Pending;
    public int Priority { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockedUntil { get; set; }
    public string? LockedByWorker { get; set; }
    public string? LastError { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public static BackgroundTask Create<T>(
        string taskType,
        T payload,
        EnqueueTaskOptions? options = null)
    {
        var now = DateTime.UtcNow;
        var opts = options ?? EnqueueTaskOptions.Default;
        var payloadJson = payload is string str ? str : JsonSerializer.Serialize(payload);

        return new BackgroundTask
        {
            Id = Guid.NewGuid(),
            TaskType = taskType.Trim(),
            PayloadJson = payloadJson,
            Status = BackgroundTaskStatus.Pending,
            Priority = opts.Priority,
            AttemptCount = 0,
            MaxAttempts = opts.MaxAttempts > 0 ? opts.MaxAttempts : 3,
            NextAttemptAt = opts.Delay.HasValue ? now.Add(opts.Delay.Value) : now,
            DeduplicationKey = opts.DeduplicationKey,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkRunning(string workerId, TimeSpan leaseDuration)
    {
        var now = DateTime.UtcNow;
        Status = BackgroundTaskStatus.Running;
        LockedByWorker = workerId;
        LockedUntil = now.Add(leaseDuration);
        AttemptCount += 1;
        UpdatedAt = now;
    }

    public void MarkCompleted()
    {
        var now = DateTime.UtcNow;
        Status = BackgroundTaskStatus.Completed;
        LockedByWorker = null;
        LockedUntil = null;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void MarkRetry(string error, TimeSpan retryDelay)
    {
        var now = DateTime.UtcNow;
        Status = BackgroundTaskStatus.Pending;
        LockedByWorker = null;
        LockedUntil = null;
        LastError = error;
        NextAttemptAt = now.Add(retryDelay);
        UpdatedAt = now;
    }

    public void MarkFailed(string error)
    {
        var now = DateTime.UtcNow;
        Status = BackgroundTaskStatus.Failed;
        LockedByWorker = null;
        LockedUntil = null;
        LastError = error;
        UpdatedAt = now;
    }

    public void MarkCancelled()
    {
        var now = DateTime.UtcNow;
        Status = BackgroundTaskStatus.Cancelled;
        LockedByWorker = null;
        LockedUntil = null;
        UpdatedAt = now;
    }
}
