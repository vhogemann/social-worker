using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public interface ICommentEnrichmentService
{
    Task<EnrichedCommentContext> EnrichCommentAsync(
        PlatformCommentItem comment,
        AppUser user,
        CancellationToken ct = default);
}
