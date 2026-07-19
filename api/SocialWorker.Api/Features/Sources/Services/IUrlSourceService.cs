using System;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Sources;

public interface IUrlSourceService
{
    Task<AddUrlSourceResult> AddUrlSourceAsync(
        Guid userId,
        Guid draftId,
        string reference,
        string? title,
        string? content,
        CancellationToken ct);
}
