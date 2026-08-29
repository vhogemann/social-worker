using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.BackgroundTasks.Abstractions;

public interface IBackgroundTaskHandler
{
    string TaskType { get; }
    TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    int DefaultMaxAttempts => 3;

    Task<TaskResult> ExecuteRawAsync(string payloadJson, ITaskExecutionContext context, CancellationToken ct);
}

public interface IBackgroundTaskHandler<TPayload> : IBackgroundTaskHandler
{
    Task<TaskResult> ExecuteAsync(TPayload payload, ITaskExecutionContext context, CancellationToken ct);
}

public abstract class BackgroundTaskHandlerBase<TPayload> : IBackgroundTaskHandler<TPayload>
{
    public abstract string TaskType { get; }
    public virtual TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    public virtual int DefaultMaxAttempts => 3;

    public abstract Task<TaskResult> ExecuteAsync(TPayload payload, ITaskExecutionContext context, CancellationToken ct);

    public virtual async Task<TaskResult> ExecuteRawAsync(string payloadJson, ITaskExecutionContext context, CancellationToken ct)
    {
        TPayload? payload;
        try
        {
            if (typeof(TPayload) == typeof(string))
            {
                payload = (TPayload)(object)payloadJson;
            }
            else
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<TPayload>(payloadJson, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception ex)
        {
            return TaskResult.Fatal($"Failed to deserialize payload for task type '{TaskType}': {ex.Message}");
        }

        if (payload == null)
        {
            return TaskResult.Fatal($"Payload deserialized to null for task type '{TaskType}'");
        }

        return await ExecuteAsync(payload, context, ct);
    }
}
