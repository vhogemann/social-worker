using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.BackgroundTasks.Services;

public sealed class BackgroundTaskRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundTaskRetentionService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public BackgroundTaskRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundTaskRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PruneOldTasksAsync(stoppingToken);
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during background tasks retention pruning");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    public async Task<int> PruneOldTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var completedCutoff = now.AddHours(-24);
        var failedCutoff = now.AddDays(-14);

        var staleTasks = await db.BackgroundTasks
            .Where(t => (t.Status == BackgroundTaskStatus.Completed && t.CompletedAt < completedCutoff) ||
                        ((t.Status == BackgroundTaskStatus.Failed || t.Status == BackgroundTaskStatus.Cancelled) && t.UpdatedAt < failedCutoff))
            .ToListAsync(ct);

        if (staleTasks.Count > 0)
        {
            db.BackgroundTasks.RemoveRange(staleTasks);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Pruned {Count} stale background task records from database", staleTasks.Count);
        }

        return staleTasks.Count;
    }
}
