using System;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.BackgroundTasks.Abstractions;

public interface ITaskExecutionContext
{
    Guid TaskId { get; }
    string TaskType { get; }
    int AttemptCount { get; }
    int MaxAttempts { get; }
    CancellationToken CancellationToken { get; }

    Task<bool> ExtendLockAsync(TimeSpan additionalTime, CancellationToken ct = default);
}
