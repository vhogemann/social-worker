using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Infrastructure.Background;

public sealed class BackgroundJobHostedService : BackgroundService
{
    private readonly BackgroundJobQueue _queue;
    private readonly ILogger<BackgroundJobHostedService> _log;

    public BackgroundJobHostedService(BackgroundJobQueue queue, ILogger<BackgroundJobHostedService> log)
    {
        _queue = queue;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Background job service started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.ReadAsync(stoppingToken);
                _log.LogInformation("Starting background job: {JobName}", job.Name);
                await job.Work(stoppingToken);
                _log.LogInformation("Completed background job: {JobName}", job.Name);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Background job failed");
            }
        }
        _log.LogInformation("Background job service stopped");
    }
}