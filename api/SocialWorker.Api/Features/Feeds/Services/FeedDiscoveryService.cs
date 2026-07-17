using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using HtmlAgilityPack;

namespace SocialWorker.Api.Features.Feeds;

public sealed record FeedDiscoveryResult(
    string FeedUrl,
    string? Title,
    string? WebsiteUrl,
    bool Success,
    string? Error);

public sealed class FeedDiscoveryService
{
    private readonly HttpClient _client;
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

    public FeedDiscoveryService(HttpClient client)
    {
        _client = client;
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

        // 1. Check YouTube channel/playlist URLs first for deterministic feed resolution.
        if (IsYouTubeHost(uri.Host))
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
                return new FeedDiscoveryResult(normalizedUrl, null, null, false, $"Failed to resolve YouTube channel feed: {ex.Message}");
            }
        }

        // 2. Fetch page and attempt feed reading using our HttpClient
        try
        {
            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _client.GetAsync(normalizedUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new FeedDiscoveryResult(normalizedUrl, null, null, false, $"Failed to fetch URL: HTTP {(int)response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();

            // Try parsing directly first (if it's already an RSS feed)
            try
            {
                var feed = FeedReader.ReadFromString(content);
                return new FeedDiscoveryResult(normalizedUrl, feed.Title, feed.Link ?? normalizedUrl, true, null);
            }
            catch
            {
                // Parse HTML to discover linked feed URLs
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                var linkNodes = doc.DocumentNode.SelectNodes("//link[@rel='alternate']");
                if (linkNodes != null)
                {
                    var feedLinkNode = linkNodes.FirstOrDefault(n =>
                    {
                        var type = n.GetAttributeValue("type", "").ToLowerInvariant();
                        return type == "application/rss+xml" || type == "application/atom+xml";
                    });

                    if (feedLinkNode != null)
                    {
                        var feedUrl = feedLinkNode.GetAttributeValue("href", "");
                        if (!string.IsNullOrWhiteSpace(feedUrl))
                        {
                            if (!feedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                !feedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                var baseUri = new Uri(normalizedUrl);
                                feedUrl = new Uri(baseUri, feedUrl).ToString();
                            }

                            // Fetch the actual feed content to get the title
                            var feedResponse = await _client.GetStringAsync(feedUrl);
                            var feed = FeedReader.ReadFromString(feedResponse);

                            return new FeedDiscoveryResult(feedUrl, feed.Title, normalizedUrl, true, null);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return new FeedDiscoveryResult(normalizedUrl, null, null, false, $"Feed discovery failed: {ex.Message}");
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

        // youtu.be links typically identify a video, so resolve author_url via oEmbed first.
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

    private static bool IsYouTubeHost(string host)
    {
        return host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
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

    private static string? ExtractYouTubeChannelIdFromHtml(string html)
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
                ?.GetAttributeValue("href", null);

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
        catch
        {
            // Best-effort fallback only.
        }

        return null;
    }
}
