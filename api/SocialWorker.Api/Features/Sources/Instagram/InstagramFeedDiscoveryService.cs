using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Sources.Instagram;

public sealed class InstagramFeedDiscoveryService
{
    private readonly IInstagramDiscoveryClient? _discoveryClient;
    private readonly IInstagramCredentialProvider? _credentialProvider;
    private readonly ILogger<InstagramFeedDiscoveryService>? _logger;

    public InstagramFeedDiscoveryService(
        IInstagramDiscoveryClient? discoveryClient = null,
        IInstagramCredentialProvider? credentialProvider = null,
        ILogger<InstagramFeedDiscoveryService>? logger = null)
    {
        _discoveryClient = discoveryClient;
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public bool CanHandle(Uri uri, string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return false;
        if (rawUrl.Trim().StartsWith("instagram://", StringComparison.OrdinalIgnoreCase)) return true;
        if (uri.Host.Contains("instagram.com", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public async Task<Features.Feeds.FeedDiscoveryResult> DiscoverAsync(string normalizedUrl, Uri uri, CancellationToken ct = default)
    {
        var username = InstagramUrlHelper.ExtractUsername(normalizedUrl);
        if (string.IsNullOrWhiteSpace(username))
        {
            return new Features.Feeds.FeedDiscoveryResult(normalizedUrl, null, null, false, "Could not extract a valid Instagram username from URL.");
        }

        var feedUrl = InstagramUrlHelper.NormalizeFeedUrl(username);
        var webUrl = InstagramUrlHelper.GetWebUrl(username);
        var title = $"@{username} (Instagram)";

        if (_discoveryClient != null && _credentialProvider != null)
        {
            try
            {
                var creds = await _credentialProvider.GetCredentialsAsync(null, ct);
                if (creds != null)
                {
                    var profile = await _discoveryClient.GetProfileWithMediaAsync(creds.CallerIgUserId, username, creds.AccessToken, 1, ct);
                    if (profile != null)
                    {
                        var displayName = !string.IsNullOrWhiteSpace(profile.Name) ? $"{profile.Name} (@{profile.Username})" : $"@{profile.Username}";
                        title = $"{displayName} (Instagram)";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Could not enrich Instagram discovery title for @{Username}", username);
            }
        }

        return new Features.Feeds.FeedDiscoveryResult(feedUrl, title, webUrl, true, null);
    }
}
