using System;

namespace SocialWorker.Api.Features.BackgroundTasks.Models;

public sealed record EnqueueTaskOptions
{
    public int Priority { get; init; } = 0;
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan? Delay { get; init; }
    public TimeSpan? CustomLeaseTimeout { get; init; }
    public string? DeduplicationKey { get; init; }

    public static EnqueueTaskOptions Default { get; } = new();

    public static EnqueueTaskOptions WithDelay(TimeSpan delay) =>
        new() { Delay = delay };

    public static EnqueueTaskOptions WithPriority(int priority) =>
        new() { Priority = priority };

    public static EnqueueTaskOptions WithDeduplication(string deduplicationKey) =>
        new() { DeduplicationKey = deduplicationKey };
}
