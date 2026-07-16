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
        var channelHtml = @"<html><body><link rel=""alternate"" type=""application/rss+xml"" title=""YouTube Video Feed"" href=""https://www.youtube.com/feeds/videos.xml?channel_id=UC12345"" /></body></html>";
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
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UC12345", result.FeedUrl);
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
