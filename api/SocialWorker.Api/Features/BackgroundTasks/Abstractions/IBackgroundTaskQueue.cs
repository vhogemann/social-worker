using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.BackgroundTasks.Abstractions;

public interface IBackgroundTaskQueue
{
    Task<Guid> EnqueueAsync<T>(string taskType, T payload, EnqueueTaskOptions? options = null, CancellationToken ct = default);
    Task<BackgroundTask?> DequeueNextAsync(string workerId, TimeSpan defaultLease, IReadOnlyCollection<string>? allowedTaskTypes = null, CancellationToken ct = default);
    Task MarkCompletedAsync(Guid taskId, CancellationToken ct = default);
    Task MarkRetryAsync(Guid taskId, string error, TimeSpan retryDelay, CancellationToken ct = default);
    Task MarkFailedAsync(Guid taskId, string error, CancellationToken ct = default);
    Task<bool> ExtendLeaseAsync(Guid taskId, string workerId, TimeSpan extension, CancellationToken ct = default);
    Task<bool> CancelTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<bool> RetryTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default);
    IAsyncEnumerable<bool> WaitForWorkSignalAsync(CancellationToken ct);
}
