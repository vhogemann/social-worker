using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Scheduling.Services;

public interface IScheduleQueueService
{
    Task<ScheduleQueueItem> EnqueueAsync(Guid scheduleId, Guid platformThreadId, Guid draftId, int? targetPosition = null, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleQueueItem>> ReorderQueueAsync(Guid scheduleId, IReadOnlyList<Guid> itemIdsInOrder, CancellationToken ct = default);
    Task<bool> RemoveFromQueueAsync(Guid scheduleId, Guid queueItemId, CancellationToken ct = default);
    Task<ScheduleQueueItem?> PauseQueueItemAsync(Guid scheduleId, Guid queueItemId, bool isPaused, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleQueueItem>> GetQueueAsync(Guid scheduleId, CancellationToken ct = default);
}
