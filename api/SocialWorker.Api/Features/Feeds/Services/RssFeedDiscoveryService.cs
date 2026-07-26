using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Feeds;

public sealed class RssFeedDiscoveryService
{
    private readonly HttpClient _client;
    private readonly ILogger<RssFeedDiscoveryService>? _logger;

    public RssFeedDiscoveryService(HttpClient client, ILogger<RssFeedDiscoveryService>? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<FeedDiscoveryResult> DiscoverAsync(string normalizedUrl)
    {
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

            try
            {
                var feed = FeedReader.ReadFromString(content);
                return new FeedDiscoveryResult(normalizedUrl, feed.Title, feed.Link ?? normalizedUrl, true, null);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Direct feed parsing failed for {Url}; falling back to HTML link discovery.", normalizedUrl);

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
            _logger?.LogError(ex, "Feed discovery failed for {Url}", normalizedUrl);
            return new FeedDiscoveryResult(normalizedUrl, null, null, false, $"Feed discovery failed: {ex.Message}");
        }

        return new FeedDiscoveryResult(normalizedUrl, null, null, false, "No feed found at the provided URL.");
    }
}
