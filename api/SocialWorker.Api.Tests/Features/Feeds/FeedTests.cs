using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedTests : SqliteTestBase
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFunc)
        {
            _responseFunc = responseFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFunc(request));
        }
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeChannel_ResolvesFeedUrl()
    {
        var channelHtml = @"<html><body><script>var ytData = {""externalId"":""UC12345AbCdEfGhIjKlMnOp""};</script></body></html>";
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(channelHtml)
            };
        });
        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/@somechannel");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UC12345AbCdEfGhIjKlMnOp", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeHandle_EscapedBrowseId_ResolvesFeedUrl()
    {
        var channelHtml = "<html><body><script>var data = {\\\"browseId\\\":\\\"UCescapedBrowseId123456789\\\"};</script></body></html>";
        var handler = new MockHttpMessageHandler(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(channelHtml)
            };
        });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/@somechannel");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UCescapedBrowseId123456789", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeHandle_FallsBackToAboutPage()
    {
        var baseHtml = "<html><body>No channel markers here</body></html>";
        var aboutHtml = @"<html><head><link rel=""canonical"" href=""https://www.youtube.com/channel/UCaboutFallbackChannel12345"" /></head><body></body></html>";

        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("/@somechannel/about", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(aboutHtml)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(baseHtml)
            };
        });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/@somechannel");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UCaboutFallbackChannel12345", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeHandle_FallsBackToPbjPayload()
    {
        var baseHtml = "<html><body>No markers</body></html>";
        var pbjPayload = """
            [{"response":{"header":{"c4TabbedHeaderRenderer":{"channelId":"UCpbjFallbackChannel123456"}}}}]
            """;

        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("pbj=1", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(pbjPayload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(baseHtml)
            };
        });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/@somechannel");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UCpbjFallbackChannel123456", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeChannelUrl_UsesChannelIdWithoutHtmlFetch()
    {
        var callCount = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            };
        });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/channel/UCabcdefghijklmnopQRSTUV/videos");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UCabcdefghijklmnopQRSTUV", result.FeedUrl);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubePlaylistUrl_ResolvesPlaylistFeedUrl()
    {
        var handler = new MockHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/playlist?list=PL1234567890ABCDEFG");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?playlist_id=PL1234567890ABCDEFG", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeWatchWithList_ResolvesPlaylistFeedUrl()
    {
        var handler = new MockHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/watch?v=abc123&list=PLfeedId987654321");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?playlist_id=PLfeedId987654321", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTuBeVideo_ResolvesChannelFeedViaOEmbed()
    {
        var oEmbedJson = """
            {
              "title": "Some video",
              "author_name": "Some Channel",
              "author_url": "https://www.youtube.com/@somechannel"
            }
            """;
        var authorHtml = @"<html><body><script>var ytData = {""browseId"":""UCshortLinkChannel12345678""};</script></body></html>";

        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? "";
            if (url.StartsWith("https://www.youtube.com/oembed?", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(oEmbedJson)
                };
            }

            if (url.Equals("https://www.youtube.com/@somechannel", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(authorHtml)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://youtu.be/dQw4w9WgXcQ");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UCshortLinkChannel12345678", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeLegacyUserUrl_ResolvesChannelFeedFromHtml()
    {
        var userPageHtml = @"<html><body><script>var ytData = {""channelId"":""UClegacyUserChannelId123456""};</script></body></html>";
        var handler = new MockHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(userPageHtml)
            });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://www.youtube.com/user/TLDRnews");

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UClegacyUserChannelId123456", result.FeedUrl);
    }

    [Fact]
    public async Task DiscoverAsync_StandardPageWithRssFeed_ResolvesFeedUrl()
    {
        var pageHtml = @"
            <html>
                <head>
                    <link rel=""alternate"" type=""application/rss+xml"" href=""/rss.xml"" title=""RSS Feed"" />
                </head>
            </html>";
        var feedXml = @"
            <rss version=""2.0"">
                <channel>
                    <title>My Blog</title>
                    <link>https://example.com</link>
                </channel>
            </rss>";

        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? "";
            if (url.EndsWith("rss.xml"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(feedXml)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pageHtml)
            };
        });

        var client = new HttpClient(handler);
        var discoveryService = new FeedDiscoveryService(client);

        var result = await discoveryService.DiscoverAsync("https://example.com");

        Assert.True(result.Success);
        Assert.Equal("https://example.com/rss.xml", result.FeedUrl);
        Assert.Equal("My Blog", result.Title);
    }
}
