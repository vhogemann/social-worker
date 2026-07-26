using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedPollingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedPollingHostedService> _logger;
    private readonly TimeSpan _pollingInterval;

    public FeedPollingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<FeedPollingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalMinutes = config.GetValue<double>("Feeds:PollingIntervalMinutes", 30);
        _pollingInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feed polling hosted service starting with interval: {Interval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<FeedPollingService>();
                await service.PollAllSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during feed polling run.");
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
