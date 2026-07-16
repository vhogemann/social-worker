using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public interface IBlueskyReplyTargetResolver
{
    Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct);
    Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct);
}
