using System;
using System.Net.Http;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Feeds.Services;
using SocialWorker.Api.Features.Sources.Instagram;

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
    private readonly RssHubFeedDiscoveryService _rssHubDiscoveryService;

    public FeedDiscoveryService(HttpClient client)
        : this(new YouTubeFeedDiscoveryService(client), new RssFeedDiscoveryService(client), new RssHubFeedDiscoveryService())
    {
    }

    public FeedDiscoveryService(
        YouTubeFeedDiscoveryService youTubeDiscoveryService,
        RssFeedDiscoveryService rssDiscoveryService,
        RssHubFeedDiscoveryService? rssHubDiscoveryService = null)
    {
        _youTubeDiscoveryService = youTubeDiscoveryService;
        _rssDiscoveryService = rssDiscoveryService;
        _rssHubDiscoveryService = rssHubDiscoveryService ?? new RssHubFeedDiscoveryService();
    }

    public async Task<FeedDiscoveryResult> DiscoverAsync(string url)
    {
        var normalizedUrl = url?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return new FeedDiscoveryResult(string.Empty, null, null, false, "URL is empty.");
        }

        if (normalizedUrl.Contains("://", StringComparison.Ordinal) &&
            !normalizedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (_rssHubDiscoveryService.CanHandle(new Uri("https://dummy.local"), normalizedUrl))
            {
                return await _rssHubDiscoveryService.DiscoverAsync(normalizedUrl, new Uri("https://dummy.local"));
            }
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

        if (_rssHubDiscoveryService.CanHandle(uri, normalizedUrl))
        {
            return await _rssHubDiscoveryService.DiscoverAsync(normalizedUrl, uri);
        }

        if (_youTubeDiscoveryService.CanHandle(uri))
        {
            return await _youTubeDiscoveryService.DiscoverAsync(normalizedUrl, uri);
        }

        return await _rssDiscoveryService.DiscoverAsync(normalizedUrl);
    }
}
