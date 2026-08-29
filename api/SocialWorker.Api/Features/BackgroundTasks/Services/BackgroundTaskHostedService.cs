using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.BackgroundTasks.Services;

public sealed class BackgroundTaskHostedService : BackgroundService
{
    private const int DefaultWorkerCount = 4;
    private readonly IBackgroundTaskQueue _queue;
    private readonly ITaskHandlerResolver _resolver;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundTaskHostedService> _logger;
    private readonly int _workerCount;

    public BackgroundTaskHostedService(
        IBackgroundTaskQueue queue,
        ITaskHandlerResolver resolver,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundTaskHostedService> logger,
        IConfiguration? config = null)
    {
        _queue = queue;
        _resolver = resolver;
        _scopeFactory = scopeFactory;
        _logger = logger;

        var count = config?.GetValue<int?>("BackgroundTasks:WorkerCount") ?? DefaultWorkerCount;
        _workerCount = count < 1 ? DefaultWorkerCount : count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background task processor started with {WorkerCount} concurrent workers", _workerCount);

        var workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = $"worker-{i + 1}";
            workers[i] = RunWorkerLoopAsync(workerId, stoppingToken);
        }

        await Task.WhenAll(workers);
        _logger.LogInformation("Background task processor stopped");
    }

    private async Task RunWorkerLoopAsync(string workerId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var defaultLease = TimeSpan.FromMinutes(5);
                var task = await _queue.DequeueNextAsync(workerId, defaultLease, stoppingToken);

                if (task != null)
                {
                    _logger.LogInformation("Worker {WorkerId} picked up task {TaskId} of type {TaskType} (attempt {Attempt}/{Max})",
                        workerId, task.Id, task.TaskType, task.AttemptCount, task.MaxAttempts);

                    await ProcessTaskAsync(task, workerId, stoppingToken);
                    continue;
                }

                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker loop {WorkerId}", workerId);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task ProcessTaskAsync(
        Data.Entities.BackgroundTask task,
        string workerId,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = new TaskExecutionContext(task, workerId, _queue, stoppingToken);

        var handler = _resolver.ResolveHandler(scope.ServiceProvider, task.TaskType);
        if (handler == null)
        {
            _logger.LogError("No handler found for task type '{TaskType}'. Task {TaskId} failed permanently", task.TaskType, task.Id);
            await _queue.MarkFailedAsync(task.Id, $"No handler registered for task type '{task.TaskType}'", stoppingToken);
            return;
        }

        try
        {
            var result = await handler.ExecuteRawAsync(task.PayloadJson, context, stoppingToken);

            switch (result.Outcome)
            {
                case TaskOutcome.Success:
                    _logger.LogInformation("Task {TaskId} ({TaskType}) completed successfully", task.Id, task.TaskType);
                    await _queue.MarkCompletedAsync(task.Id, CancellationToken.None);
                    break;

                case TaskOutcome.Retry:
                    var delay = result.RetryDelay ?? CalculateBackoff(task.AttemptCount);
                    var reason = result.ErrorMessage ?? "Task requested retry";
                    if (task.AttemptCount >= task.MaxAttempts)
                    {
                        _logger.LogWarning("Task {TaskId} ({TaskType}) exceeded max attempts ({MaxAttempts}): {Reason}",
                            task.Id, task.TaskType, task.MaxAttempts, reason);
                        await _queue.MarkFailedAsync(task.Id, reason, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogWarning("Task {TaskId} ({TaskType}) failed, retrying in {DelaySeconds}s: {Reason}",
                            task.Id, task.TaskType, delay.TotalSeconds, reason);
                        await _queue.MarkRetryAsync(task.Id, reason, delay, CancellationToken.None);
                    }
                    break;

                case TaskOutcome.Fatal:
                    _logger.LogError("Task {TaskId} ({TaskType}) failed fatally: {Error}", task.Id, task.TaskType, result.ErrorMessage);
                    await _queue.MarkFailedAsync(task.Id, result.ErrorMessage ?? "Fatal task error", CancellationToken.None);
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception executing task {TaskId} ({TaskType})", task.Id, task.TaskType);
            if (task.AttemptCount >= task.MaxAttempts)
            {
                await _queue.MarkFailedAsync(task.Id, ex.Message, CancellationToken.None);
            }
            else
            {
                var delay = CalculateBackoff(task.AttemptCount);
                await _queue.MarkRetryAsync(task.Id, ex.Message, delay, CancellationToken.None);
            }
        }
    }

    private static TimeSpan CalculateBackoff(int attemptCount)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Max(1, attemptCount)) * 5);
        return TimeSpan.FromSeconds(seconds);
    }
}
