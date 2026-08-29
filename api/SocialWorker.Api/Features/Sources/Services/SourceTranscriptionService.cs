using System;
using System.Threading.Tasks;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Sources.Handlers;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceTranscriptionService
{
    private readonly IBackgroundTaskQueue _queue;

    public SourceTranscriptionService(IBackgroundTaskQueue queue)
    {
        _queue = queue;
    }

    public void QueueTranscriptExtraction(Guid sourceId, Guid draftId)
    {
        _ = _queue.EnqueueAsync("source-transcription", new SourceTranscriptionPayload(sourceId, draftId));
    }
}