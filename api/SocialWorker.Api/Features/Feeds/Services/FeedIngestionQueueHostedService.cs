using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedIngestionQueueHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedIngestionQueueHostedService> _logger;
    private readonly TimeSpan _pollingInterval;

    public FeedIngestionQueueHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<FeedIngestionQueueHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = config.GetValue<double>("Feeds:QueuePollingIntervalSeconds", 10);
        _pollingInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feed ingestion queue hosted service starting with interval: {Interval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<FeedIngestionQueueProcessor>();
                await processor.ProcessNextQueueItemAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing feed ingestion queue.");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
