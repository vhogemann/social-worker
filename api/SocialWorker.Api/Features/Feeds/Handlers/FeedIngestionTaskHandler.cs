using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.Feeds.Handlers;

public sealed record FeedIngestionPayload(
    Guid FeedSubscriptionId,
    Guid AutomationWorkflowId,
    string ItemTitle,
    string ItemLink,
    string? ItemDescription,
    DateTime? ItemPublishedAt,
    string? MediaUrl = null,
    string? MediaType = null);

public sealed class FeedIngestionTaskHandler : BackgroundTaskHandlerBase<FeedIngestionPayload>
{
    private readonly AppDbContext _db;
    private readonly FeedOrchestrationService _orchestrator;
    private readonly ILogger<FeedIngestionTaskHandler> _logger;

    public override string TaskType => "feed-ingestion";
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    public override int DefaultMaxAttempts => 3;

    public FeedIngestionTaskHandler(
        AppDbContext db,
        FeedOrchestrationService orchestrator,
        ILogger<FeedIngestionTaskHandler> logger)
    {
        _db = db;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        FeedIngestionPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var subscription = await _db.FeedSubscriptions
            .FirstOrDefaultAsync(s => s.Id == payload.FeedSubscriptionId, ct);

        if (subscription == null)
        {
            return TaskResult.Fatal($"Feed subscription {payload.FeedSubscriptionId} not found");
        }

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.Destinations)
            .FirstOrDefaultAsync(w => w.Id == payload.AutomationWorkflowId, ct);

        if (workflow == null)
        {
            return TaskResult.Fatal($"Automation workflow {payload.AutomationWorkflowId} not found");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(payload.MediaUrl) && string.IsNullOrWhiteSpace(payload.MediaType))
            {
                await _orchestrator.ProcessFeedItemAsync(
                    subscription,
                    workflow,
                    payload.ItemTitle,
                    payload.ItemLink,
                    payload.ItemDescription ?? string.Empty,
                    payload.ItemPublishedAt,
                    ct);
            }
            else
            {
                await _orchestrator.ProcessFeedItemAsync(
                    subscription,
                    workflow,
                    payload.ItemTitle,
                    payload.ItemLink,
                    payload.ItemDescription ?? string.Empty,
                    payload.ItemPublishedAt,
                    ct,
                    payload.MediaUrl,
                    payload.MediaType);
            }

            return TaskResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest feed item {ItemLink} for subscription {SubscriptionId}",
                payload.ItemLink, payload.FeedSubscriptionId);
            return TaskResult.Retry(TimeSpan.FromSeconds(60), ex.Message);
        }
    }
}
