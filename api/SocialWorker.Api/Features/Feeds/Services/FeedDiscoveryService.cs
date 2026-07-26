using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Feeds;

public sealed record FeedDiscoveryResult(
    string FeedUrl,
    string? Title,
    string? WebsiteUrl,
    bool Success,
    string? Error);

public sealed class FeedDiscoveryService
{
    private readonly YouTubeFeedDiscoveryService _youTubeDiscoveryService;
    private readonly RssFeedDiscoveryService _rssDiscoveryService;

    public FeedDiscoveryService(HttpClient client)
        : this(new YouTubeFeedDiscoveryService(client), new RssFeedDiscoveryService(client))
    {
    }

    public FeedDiscoveryService(
        YouTubeFeedDiscoveryService youTubeDiscoveryService,
        RssFeedDiscoveryService rssDiscoveryService)
    {
        _youTubeDiscoveryService = youTubeDiscoveryService;
        _rssDiscoveryService = rssDiscoveryService;
    }

    public async Task<FeedDiscoveryResult> DiscoverAsync(string url)
    {
        var normalizedUrl = url?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return new FeedDiscoveryResult(string.Empty, null, null, false, "URL is empty.");
        }

        if (!normalizedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = "https://" + normalizedUrl;
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return new FeedDiscoveryResult(normalizedUrl, null, null, false, "Invalid URL format.");
        }

        if (_youTubeDiscoveryService.CanHandle(uri))
        {
            return await _youTubeDiscoveryService.DiscoverAsync(normalizedUrl, uri);
        }

        return await _rssDiscoveryService.DiscoverAsync(normalizedUrl);
    }
}
