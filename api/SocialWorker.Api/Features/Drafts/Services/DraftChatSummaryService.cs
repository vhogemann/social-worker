using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Drafts.Handlers;

namespace SocialWorker.Api.Features.Drafts;

public sealed class DraftChatSummaryService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<DraftChatSummaryService>? _logger;

    public DraftChatSummaryService(IBackgroundTaskQueue queue, ILogger<DraftChatSummaryService>? logger = null)
    {
        _queue = queue;
        _logger = logger;
    }

    public void TriggerBackgroundSummarization(Guid userId, Guid draftId, string chatHistoryJson)
    {
        _ = _queue.EnqueueAsync("draft-chat-summary", new DraftChatSummaryPayload(userId, draftId, chatHistoryJson))
            .ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger?.LogError(t.Exception, "Failed to enqueue draft-chat-summary task for draft {DraftId}", draftId);
                }
            }, TaskScheduler.Default);
    }
}
