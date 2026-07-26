using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Feeds;

public sealed class YouTubeFeedDiscoveryService
{
    private readonly HttpClient _client;
    private readonly ILogger<YouTubeFeedDiscoveryService>? _logger;

    private static readonly string[] YouTubeChannelIdPatterns =
    [
        @"(?:https?:)?(?:\/\/)?(?:www\.)?youtube\.com/feeds/videos\.xml\?channel_id=(UC[\w-]+)",
        @"feeds/videos\.xml\?channel_id=(UC[\w-]+)",
        @"channel_id\\u003d(UC[\w-]+)",
        @"(?:https?:)?(?:\/\/)?(?:www\.)?youtube\.com/channel/(UC[\w-]+)",
        @"""channelId""\s*:\s*""(UC[\w-]+)""",
        @"\\""channelId\\""\s*:\s*\\""(UC[\w-]+)\\""",
        @"""externalId""\s*:\s*""(UC[\w-]+)""",
        @"\\""externalId\\""\s*:\s*\\""(UC[\w-]+)\\""",
        @"""browseId""\s*:\s*""(UC[\w-]+)""",
        @"\\""browseId\\""\s*:\s*\\""(UC[\w-]+)\\"""
    ];

    public YouTubeFeedDiscoveryService(HttpClient client, ILogger<YouTubeFeedDiscoveryService>? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    public bool CanHandle(Uri uri)
    {
        return uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<FeedDiscoveryResult> DiscoverAsync(string normalizedUrl, Uri uri)
    {
        try
        {
            var playlistId = TryExtractYouTubePlaylistId(uri);
            if (!string.IsNullOrWhiteSpace(playlistId))
            {
                var feedUrl = $"https://www.youtube.com/feeds/videos.xml?playlist_id={playlistId}";
                return new FeedDiscoveryResult(feedUrl, $"YouTube Playlist {playlistId}", normalizedUrl, true, null);
            }

            var channelId = await ResolveYouTubeChannelIdAsync(normalizedUrl, uri);
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
                return new FeedDiscoveryResult(feedUrl, $"YouTube Channel {channelId}", normalizedUrl, true, null);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resolve YouTube channel feed for {Url}", normalizedUrl);
            return new FeedDiscoveryResult(normalizedUrl, null, null, false, $"Failed to resolve YouTube channel feed: {ex.Message}");
        }

        return new FeedDiscoveryResult(normalizedUrl, null, null, false, "No feed found at the provided URL.");
    }

    private static string? TryExtractYouTubeChannelIdFromUrl(Uri uri)
    {
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("channel", StringComparison.OrdinalIgnoreCase) &&
                segments[i + 1].StartsWith("UC", StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }

        return null;
    }

    private static string? TryExtractYouTubePlaylistId(Uri uri)
    {
        if (uri.AbsolutePath.Contains("/playlist", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.Contains("/watch", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.Contains("/feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
        {
            var list = TryGetQueryValue(uri.Query, "list");
            if (!string.IsNullOrWhiteSpace(list))
            {
                return list;
            }

            var playlistId = TryGetQueryValue(uri.Query, "playlist_id");
            if (!string.IsNullOrWhiteSpace(playlistId))
            {
                return playlistId;
            }
        }

        return null;
    }

    private async Task<string?> ResolveYouTubeChannelIdAsync(string normalizedUrl, Uri uri)
    {
        var channelId = TryExtractYouTubeChannelIdFromUrl(uri);
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            return channelId;
        }

        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.Contains("/watch", StringComparison.OrdinalIgnoreCase))
        {
            channelId = await TryResolveChannelIdFromYouTubeOEmbedAsync(normalizedUrl);
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                return channelId;
            }
        }

        SetBrowserUserAgent();
        var channelHtml = await _client.GetStringAsync(normalizedUrl);
        channelId = ExtractYouTubeChannelIdFromHtml(channelHtml);
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            return channelId;
        }

        var aboutUrl = BuildYouTubeAboutUrl(uri);
        if (!string.IsNullOrWhiteSpace(aboutUrl) && !aboutUrl.Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase))
        {
            var aboutHtml = await _client.GetStringAsync(aboutUrl);
            channelId = ExtractYouTubeChannelIdFromHtml(aboutHtml);
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                return channelId;
            }
        }

        channelId = await TryResolveChannelIdFromYouTubePbjAsync(normalizedUrl);
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            return channelId;
        }

        return null;
    }

    private async Task<string?> TryResolveChannelIdFromYouTubePbjAsync(string url)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var pbjUrl = $"{url}{separator}pbj=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, pbjUrl);
        request.Headers.TryAddWithoutValidation("x-youtube-client-name", "1");
        request.Headers.TryAddWithoutValidation("x-youtube-client-version", "2.20240726.00.00");
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");

        var response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();
        return ExtractYouTubeChannelIdFromHtml(body);
    }

    private async Task<string?> TryResolveChannelIdFromYouTubeOEmbedAsync(string videoUrl)
    {
        var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(videoUrl)}&format=json";
        var response = await _client.GetAsync(oEmbedUrl);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("author_url", out var authorUrlElement))
        {
            return null;
        }

        var authorUrl = authorUrlElement.GetString();
        if (string.IsNullOrWhiteSpace(authorUrl) || !Uri.TryCreate(authorUrl, UriKind.Absolute, out var authorUri))
        {
            return null;
        }

        var channelId = TryExtractYouTubeChannelIdFromUrl(authorUri);
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            return channelId;
        }

        SetBrowserUserAgent();
        var authorHtml = await _client.GetStringAsync(authorUrl);
        return ExtractYouTubeChannelIdFromHtml(authorHtml);
    }

    private static string? BuildYouTubeAboutUrl(Uri uri)
    {
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (segments.Count == 0)
        {
            return null;
        }

        if (segments[^1].Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        segments.RemoveAll(s =>
            s.Equals("videos", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("playlists", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("streams", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("featured", StringComparison.OrdinalIgnoreCase));

        segments.Add("about");
        var path = "/" + string.Join('/', segments);

        var builder = new UriBuilder(uri)
        {
            Path = path,
            Query = string.Empty
        };

        return builder.Uri.ToString();
    }

    private static string? TryGetQueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }

    private void SetBrowserUserAgent()
    {
        _client.DefaultRequestHeaders.UserAgent.Clear();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    private string? ExtractYouTubeChannelIdFromHtml(string html)
    {
        foreach (var pattern in YouTubeChannelIdPatterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var canonicalHref = doc.DocumentNode
                .SelectSingleNode("//link[@rel='canonical']")
                ?.GetAttributeValue("href", string.Empty);

            if (!string.IsNullOrWhiteSpace(canonicalHref) &&
                Uri.TryCreate(canonicalHref, UriKind.Absolute, out var canonicalUri))
            {
                var fromCanonical = TryExtractYouTubeChannelIdFromUrl(canonicalUri);
                if (!string.IsNullOrWhiteSpace(fromCanonical))
                {
                    return fromCanonical;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to parse HTML canonical link for YouTube channel ID.");
        }

        return null;
    }
}
