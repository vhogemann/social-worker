using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Feeds;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class YouTubeFeedDiscoveryServiceTests
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

    [Theory]
    [InlineData("https://www.youtube.com/@somechannel", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
    [InlineData("https://example.com/rss", false)]
    public void CanHandle_ValidatesHostCorrectly(string url, bool expected)
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new YouTubeFeedDiscoveryService(new HttpClient(handler));
        var uri = new Uri(url);

        Assert.Equal(expected, service.CanHandle(uri));
    }

    [Fact]
    public async Task DiscoverAsync_YouTubeChannel_ResolvesFeedUrl()
    {
        var channelHtml = @"<html><body><script>var ytData = {""externalId"":""UC12345AbCdEfGhIjKlMnOp""};</script></body></html>";
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(channelHtml)
        });
        var client = new HttpClient(handler);
        var service = new YouTubeFeedDiscoveryService(client);
        var url = "https://www.youtube.com/@somechannel";
        var uri = new Uri(url);

        var result = await service.DiscoverAsync(url, uri);

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/feeds/videos.xml?channel_id=UC12345AbCdEfGhIjKlMnOp", result.FeedUrl);
    }
}
