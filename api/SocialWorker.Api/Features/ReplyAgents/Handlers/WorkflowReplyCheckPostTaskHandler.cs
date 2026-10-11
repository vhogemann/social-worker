using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;

namespace SocialWorker.Api.Features.ReplyAgents.Handlers;

public sealed class WorkflowReplyCheckPostTaskHandler : BackgroundTaskHandlerBase<WorkflowReplyCheckPostPayload>
{
    private readonly ReplyAgentCoordinatorService _coordinator;
    private readonly ILogger<WorkflowReplyCheckPostTaskHandler> _logger;

    public override string TaskType => "workflow-reply-check-post";
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(5);
    public override int DefaultMaxAttempts => 3;

    public WorkflowReplyCheckPostTaskHandler(
        ReplyAgentCoordinatorService coordinator,
        ILogger<WorkflowReplyCheckPostTaskHandler> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        WorkflowReplyCheckPostPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        try
        {
            await _coordinator.ProcessPostCommentsAsync(payload.WorkflowReplyAgentId, payload.PostId, ct);
            return TaskResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process reply check for agent {AgentId} and post {PostId}", payload.WorkflowReplyAgentId, payload.PostId);
            return TaskResult.Retry(TimeSpan.FromMinutes(2), ex.Message);
        }
    }
}
