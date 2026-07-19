using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Sources;

public interface IFileSourceService
{
    Task<AddFileSourceResult> AddFileSourceAsync(
        Guid userId,
        Guid draftId,
        string fileName,
        Stream fileStream,
        SourceExtractor extractor,
        CancellationToken ct);
}
