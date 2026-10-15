using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Scheduling.Services;

public sealed class ScheduleEvaluationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleEvaluationHostedService> _logger;
    private readonly TimeSpan _checkInterval;

    public ScheduleEvaluationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduleEvaluationHostedService> logger,
        TimeSpan? checkInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await EvaluateAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during periodic schedule evaluation");
            }
        }
    }

    public async Task<int> EvaluateAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<ScheduleSlotEvaluatorService>();
        var dispatched = await evaluator.EvaluateAllActiveSchedulesAsync(ct: ct);
        if (dispatched > 0)
        {
            _logger.LogInformation("Periodic schedule evaluator dispatched {Count} posts to open slots", dispatched);
        }
        return dispatched;
    }
}
