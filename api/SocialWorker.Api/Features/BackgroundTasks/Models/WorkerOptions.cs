using System.Collections.Generic;

namespace SocialWorker.Api.Features.BackgroundTasks.Models;

public sealed record WorkerOptions
{
    public string Name { get; init; } = "";
    public List<string> AllowedTaskTypes { get; init; } = new();
}

public sealed record BackgroundTasksOptions
{
    public const string SectionName = "BackgroundTasks";
    public int? WorkerCount { get; init; }
    public List<WorkerOptions>? Workers { get; init; }
}
