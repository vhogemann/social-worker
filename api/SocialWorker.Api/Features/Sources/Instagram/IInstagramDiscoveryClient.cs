using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;

namespace SocialWorker.Api.Features.Sources.Instagram;

public interface IInstagramDiscoveryClient
{
    Task<InstagramProfileDiscovery?> GetProfileWithMediaAsync(string callerIgUserId, string targetUsername, string accessToken, int limit = 10, CancellationToken ct = default);
    Task<string?> ResolveCallerIgUserIdAsync(string accessToken, CancellationToken ct = default);
    Task<Feed> GetProfileFeedAsync(string targetUsername, string callerIgUserId, string accessToken, int limit = 10, CancellationToken ct = default);
}
