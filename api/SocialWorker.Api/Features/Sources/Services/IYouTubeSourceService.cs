using System;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Sources;

public interface IYouTubeSourceService
{
    bool IsYouTubeUrl(string reference);
    string? TryExtractYouTubeVideoId(string reference);
    void QueueTranscriptExtraction(Guid sourceId, Guid draftId);
    Task<SourceStatusDto> RetrySourceTranscriptAsync(Guid userId, Guid sourceId, CancellationToken ct);
}
