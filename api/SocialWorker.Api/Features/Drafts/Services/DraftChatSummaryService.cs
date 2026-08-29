using System;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Drafts.Handlers;

namespace SocialWorker.Api.Features.Drafts;

public sealed class DraftChatSummaryService
{
    private readonly IBackgroundTaskQueue _queue;

    public DraftChatSummaryService(IBackgroundTaskQueue queue)
    {
        _queue = queue;
    }

    public void TriggerBackgroundSummarization(Guid userId, Guid draftId, string chatHistoryJson)
    {
        _ = _queue.EnqueueAsync("draft-chat-summary", new DraftChatSummaryPayload(userId, draftId, chatHistoryJson));
    }
}
