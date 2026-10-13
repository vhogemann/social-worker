using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public interface IReplyGenerationService
{
    Task<GeneratedReplyResult> GenerateReplyAsync(
        EnrichedCommentContext enrichedContext,
        string originalPostContent,
        string personaPrompt,
        string platform,
        string? threadSummary,
        AppUser user,
        Guid? draftId = null,
        CancellationToken ct = default);
}
