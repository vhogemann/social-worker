using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Infrastructure.Background;

public sealed class BackgroundJobHostedService : BackgroundService
{
    private const int DefaultWorkerCount = 4;
    private readonly BackgroundJobQueue _queue;
    private readonly ILogger<BackgroundJobHostedService> _log;
    private readonly int _workerCount;

    public BackgroundJobHostedService(
        BackgroundJobQueue queue,
        ILogger<BackgroundJobHostedService> log,
        IConfiguration? config = null)
    {
        _queue = queue;
        _log = log;
        var count = config?.GetValue<int?>("BackgroundJobs:WorkerCount") ?? DefaultWorkerCount;
        _workerCount = count < 1 ? DefaultWorkerCount : count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Background job service started with {WorkerCount} concurrent workers", _workerCount);

        var workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i + 1;
            workers[i] = RunWorkerAsync(workerId, stoppingToken);
        }

        await Task.WhenAll(workers);
        _log.LogInformation("Background job service stopped");
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.ReadAsync(stoppingToken);
                _log.LogInformation("Worker {WorkerId} starting background job: {JobName}", workerId, job.Name);
                await job.Work(stoppingToken);
                _log.LogInformation("Worker {WorkerId} completed background job: {JobName}", workerId, job.Name);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Worker {WorkerId} background job failed", workerId);
            }
        }
    }
}