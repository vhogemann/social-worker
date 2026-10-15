using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Sources.Instagram;

namespace SocialWorker.Api.Features.Feeds.Services;

public class RssHubFeedDiscoveryService
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const string RedditUserAgent = "social-worker/1.0 (feed-reader; contact: admin@social-worker.local)";

    private readonly IRssHubRouteResolver _routeResolver;
    private readonly IInstagramDiscoveryClient? _instagramClient;
    private readonly IInstagramCredentialProvider? _instagramCredentialProvider;
    private readonly ILogger<RssHubFeedDiscoveryService>? _logger;

    public RssHubFeedDiscoveryService(
        IRssHubRouteResolver? routeResolver = null,
        IInstagramDiscoveryClient? instagramClient = null,
        IInstagramCredentialProvider? instagramCredentialProvider = null,
        ILogger<RssHubFeedDiscoveryService>? logger = null)
    {
        _routeResolver = routeResolver ?? new RssHubRouteResolver();
        _instagramClient = instagramClient;
        _instagramCredentialProvider = instagramCredentialProvider;
        _logger = logger;
    }

    public bool CanHandle(Uri uri, string rawUrl)
    {
        return _routeResolver.TryResolve(rawUrl, out _);
    }

    public async Task<FeedDiscoveryResult> DiscoverAsync(string normalizedUrl, Uri uri, CancellationToken ct = default)
    {
        if (!_routeResolver.TryResolve(normalizedUrl, out var match))
        {
            return new FeedDiscoveryResult(normalizedUrl, null, null, false, "URL does not match any supported platform route.");
        }

        var rssHubUrl = $"{_routeResolver.GetBaseUrl()}{match.RoutePath}";

        try
        {
            var feed = await FeedReader.ReadAsync(rssHubUrl, ct, autoRedirect: true, userAgent: BrowserUserAgent);
            if (!string.IsNullOrWhiteSpace(feed?.Title))
            {
                var title = feed.Title.Trim();
                if (!title.Contains(match.Platform, StringComparison.OrdinalIgnoreCase))
                {
                    title = $"{title} ({match.Platform})";
                }

                return new FeedDiscoveryResult(match.CanonicalFeedUrl, title, match.WebUrl, true, null);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Live RSSHub probe failed for {RoutePath}; attempting fallback.", match.RoutePath);
        }

        if (string.Equals(match.Platform, "Instagram", StringComparison.OrdinalIgnoreCase) &&
            _instagramClient != null && _instagramCredentialProvider != null)
        {
            try
            {
                var creds = await _instagramCredentialProvider.GetCredentialsAsync(null, ct);
                if (creds != null)
                {
                    var username = match.CanonicalFeedUrl.Replace("instagram://", "", StringComparison.OrdinalIgnoreCase);
                    var profile = await _instagramClient.GetProfileWithMediaAsync(creds.CallerIgUserId, username, creds.AccessToken, 1, ct);
                    if (profile != null)
                    {
                        var displayName = !string.IsNullOrWhiteSpace(profile.Name) ? $"{profile.Name} (@{profile.Username})" : $"@{profile.Username}";
                        return new FeedDiscoveryResult(match.CanonicalFeedUrl, $"{displayName} (Instagram)", match.WebUrl, true, null);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Could not enrich Instagram discovery title via Graph API for {FeedUrl}", match.CanonicalFeedUrl);
            }
        }

        if ((string.Equals(match.Platform, "Reddit", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(match.Platform, "RedditUser", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(match.WebUrl))
        {
            try
            {
                var redditRssUrl = $"{match.WebUrl.TrimEnd('/')}/.rss";
                var feed = await FeedReader.ReadAsync(redditRssUrl, ct, autoRedirect: true, userAgent: RedditUserAgent);
                if (!string.IsNullOrWhiteSpace(feed?.Title))
                {
                    var title = feed.Title.Trim();
                    if (!title.Contains("Reddit", StringComparison.OrdinalIgnoreCase))
                    {
                        title = $"{title} (Reddit)";
                    }
                    return new FeedDiscoveryResult(match.CanonicalFeedUrl, title, match.WebUrl, true, null);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Live Reddit RSS probe failed for {WebUrl}", match.WebUrl);
            }
        }

        var handle = ExtractHandle(match.CanonicalFeedUrl);
        var fallbackTitle = string.IsNullOrWhiteSpace(handle) ? match.Platform : $"@{handle} ({match.Platform})";

        return new FeedDiscoveryResult(match.CanonicalFeedUrl, fallbackTitle, match.WebUrl, true, null);
    }

    private static string ExtractHandle(string canonicalFeedUrl)
    {
        var colonIndex = canonicalFeedUrl.IndexOf("://", StringComparison.Ordinal);
        if (colonIndex < 0) return canonicalFeedUrl;

        var remainder = canonicalFeedUrl[(colonIndex + 3)..].Trim('/');
        return remainder.StartsWith("r/", StringComparison.OrdinalIgnoreCase) ? remainder : remainder;
    }
}
