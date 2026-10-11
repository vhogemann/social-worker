using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public interface ICommentRankingService
{
    Task<CommentRankingResult> RankCommentAsync(
        EnrichedCommentContext enrichedContext,
        string originalPostContent,
        string? threadSummary,
        AppUser user,
        CancellationToken ct = default);
}
