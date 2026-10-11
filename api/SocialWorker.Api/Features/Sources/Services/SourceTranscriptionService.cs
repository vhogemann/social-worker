using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Sources.Handlers;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceTranscriptionService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<SourceTranscriptionService>? _logger;

    public SourceTranscriptionService(IBackgroundTaskQueue queue, ILogger<SourceTranscriptionService>? logger = null)
    {
        _queue = queue;
        _logger = logger;
    }

    public void QueueTranscriptExtraction(Guid sourceId, Guid draftId, string? mediaUrl = null)
    {
        _ = _queue.EnqueueAsync("source-transcription", new SourceTranscriptionPayload(sourceId, draftId, mediaUrl))
            .ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger?.LogError(t.Exception, "Failed to enqueue source-transcription task for source {SourceId}", sourceId);
                }
            }, TaskScheduler.Default);
    }
}