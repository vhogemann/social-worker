namespace SocialWorker.Api.Features.BackgroundTasks.Models;

public enum BackgroundTaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
