using System;
using System.Linq;
using System.Net.Http;
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

        // 1. Check YouTube channel URL or handle
        if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) && 
            (normalizedUrl.Contains("/@", StringComparison.OrdinalIgnoreCase) || normalizedUrl.Contains("/channel/", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                string? channelId = null;
                if (normalizedUrl.Contains("/channel/UC", StringComparison.OrdinalIgnoreCase))
                {
                    var index = normalizedUrl.IndexOf("/channel/", StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        channelId = normalizedUrl.Substring(index + 9).Split('/')[0].Split('?')[0];
                    }
                }
                else
                {
                    _client.DefaultRequestHeaders.UserAgent.Clear();
                    _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    var channelHtml = await _client.GetStringAsync(normalizedUrl);
                    var match = Regex.Match(channelHtml, @"youtube\.com/feeds/videos\.xml\?channel_id=(UC[\w-]+)");
                    if (match.Success)
                    {
                        channelId = match.Groups[1].Value;
                    }
                }

                if (!string.IsNullOrEmpty(channelId))
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
}
