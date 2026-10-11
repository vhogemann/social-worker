using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents;

public sealed class ReplyAgentPollingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _pollingInterval;
    private readonly ILogger<ReplyAgentPollingHostedService> _logger;

    public ReplyAgentPollingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ReplyAgentPollingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalMinutes = config.GetValue<double>("ReplyAgents:PollingIntervalMinutes", 5);
        _pollingInterval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reply agent polling hosted service starting with interval: {Interval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var taskQueue = scope.ServiceProvider.GetRequiredService<IBackgroundTaskQueue>();

                var agents = await db.WorkflowReplyAgents
                    .Where(a => a.IsEnabled)
                    .ToListAsync(stoppingToken);

                foreach (var agent in agents)
                {
                    var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(agent.MaxPostAgeDays));
                    var posts = await db.Posts
                        .Include(p => p.Draft)
                        .Where(p => p.Draft.AutomationWorkflowId == agent.AutomationWorkflowId
                            && p.RemoteId != null
                            && p.PostedAt >= cutoff)
                        .ToListAsync(stoppingToken);

                    foreach (var post in posts)
                    {
                        var dedupKey = $"reply-check-{agent.Id}-{post.Id}-{DateTime.UtcNow:yyyyMMddHHmm}";
                        var payload = new WorkflowReplyCheckPostPayload(agent.Id, post.Id);
                        var options = EnqueueTaskOptions.WithDeduplication(dedupKey);

                        await taskQueue.EnqueueAsync("workflow-reply-check-post", payload, options, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during reply agent polling tick.");
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
