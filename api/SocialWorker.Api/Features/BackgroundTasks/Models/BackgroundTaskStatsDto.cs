using System.Collections.Generic;

namespace SocialWorker.Api.Features.BackgroundTasks.Models;

public sealed record BackgroundTaskStatsDto(
    int TotalPending,
    int TotalRunning,
    int TotalCompleted,
    int TotalFailed,
    int TotalCancelled,
    IReadOnlyDictionary<string, int> CountsByTaskType);
