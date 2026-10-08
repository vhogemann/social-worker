using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.MetaThreads;

public sealed class MetaThreadsApiClientTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responseFactory(request);
        }
    }

    private static MetaThreadsApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> factory, MetaThreadsOptions? options = null)
    {
        var handler = new FakeHttpMessageHandler(factory);
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? new MetaThreadsOptions
        {
            BaseUrl = "https://graph.threads.net/v1.0"
        });
        return new MetaThreadsApiClient(httpClient, opts, NullLogger<MetaThreadsApiClient>.Instance);
    }

    [Fact]
    public async Task CreateTextContainerAsync_SendsCorrectParameters()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"container_123\"}")
            };
        });
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(new MetaThreadsOptions());
        var client = new MetaThreadsApiClient(httpClient, opts, NullLogger<MetaThreadsApiClient>.Instance);

        var result = await client.CreateTextContainerAsync("user_1", "token_abc", "Hello Threads", "reply_99");

        Assert.Equal("container_123", result.Id);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("media_type=TEXT", handler.LastRequestBody);
        Assert.Contains("text=Hello+Threads", handler.LastRequestBody);
        Assert.Contains("access_token=token_abc", handler.LastRequestBody);
        Assert.Contains("reply_to_id=reply_99", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateImageContainerAsync_ForCarouselItem_SetsCarouselFlag()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"item_456\"}")
            };
        });
        var httpClient = new HttpClient(handler);
        var client = new MetaThreadsApiClient(httpClient, Microsoft.Extensions.Options.Options.Create(new MetaThreadsOptions()), NullLogger<MetaThreadsApiClient>.Instance);

        var result = await client.CreateImageContainerAsync("user_1", "token_abc", "https://example.com/pic.jpg", altText: "An image", isCarouselItem: true);

        Assert.Equal("item_456", result.Id);
        Assert.Contains("media_type=IMAGE", handler.LastRequestBody);
        Assert.Contains("is_carousel_item=true", handler.LastRequestBody);
        Assert.Contains("image_url=https%3A%2F%2Fexample.com%2Fpic.jpg", handler.LastRequestBody);
        Assert.Contains("alt_text=An+image", handler.LastRequestBody);
        Assert.DoesNotContain("reply_to_id", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateCarouselContainerAsync_SendsChildrenCommaSeparated()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"carousel_789\"}")
            };
        });
        var httpClient = new HttpClient(handler);
        var client = new MetaThreadsApiClient(httpClient, Microsoft.Extensions.Options.Options.Create(new MetaThreadsOptions()), NullLogger<MetaThreadsApiClient>.Instance);

        var children = new[] { "item_1", "item_2", "item_3" };
        var result = await client.CreateCarouselContainerAsync("user_1", "token_abc", children, text: "Check out this gallery", replyToId: "reply_0");

        Assert.Equal("carousel_789", result.Id);
        Assert.Contains("media_type=CAROUSEL", handler.LastRequestBody);
        Assert.Contains("children=item_1%2Citem_2%2Citem_3", handler.LastRequestBody);
        Assert.Contains("text=Check+out+this+gallery", handler.LastRequestBody);
        Assert.Contains("reply_to_id=reply_0", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateCarouselContainerAsync_LessThanTwoItems_ThrowsArgumentException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreateCarouselContainerAsync("user_1", "token", new[] { "item_1" }));
    }

    [Fact]
    public async Task GetContainerStatusAsync_ReturnsStatusAndErrorMessage()
    {
        var client = CreateClient(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"c_1\",\"status\":\"FINISHED\",\"error_message\":null}")
            };
        });

        var result = await client.GetContainerStatusAsync("c_1", "token");

        Assert.Equal("c_1", result.Id);
        Assert.Equal("FINISHED", result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task WaitForContainerReadyAsync_WhenStatusBecomesFinished_ReturnsTrue()
    {
        int callCount = 0;
        var client = CreateClient(req =>
        {
            callCount++;
            var status = callCount >= 2 ? "FINISHED" : "IN_PROGRESS";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"id\":\"c_1\",\"status\":\"{status}\"}}")
            };
        });

        var ready = await client.WaitForContainerReadyAsync(
            "c_1",
            "token",
            timeout: TimeSpan.FromSeconds(5),
            initialDelay: TimeSpan.FromMilliseconds(1),
            delayFunc: (_, _) => Task.CompletedTask);

        Assert.True(ready);
        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task WaitForContainerReadyAsync_WhenStatusIsError_ThrowsMetaThreadsApiException()
    {
        var client = CreateClient(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"c_1\",\"status\":\"ERROR\",\"error_message\":\"Media download failed\"}")
            };
        });

        var ex = await Assert.ThrowsAsync<MetaThreadsApiException>(() =>
            client.WaitForContainerReadyAsync(
                "c_1",
                "token",
                timeout: TimeSpan.FromSeconds(5),
                delayFunc: (_, _) => Task.CompletedTask));

        Assert.Contains("Media download failed", ex.Message);
    }

    [Fact]
    public async Task PublishContainerAsync_ReturnsPublishedPostId()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"post_published_999\"}")
            };
        });
        var httpClient = new HttpClient(handler);
        var client = new MetaThreadsApiClient(httpClient, Microsoft.Extensions.Options.Options.Create(new MetaThreadsOptions()), NullLogger<MetaThreadsApiClient>.Instance);

        var result = await client.PublishContainerAsync("user_1", "token_abc", "container_123");

        Assert.Equal("post_published_999", result.Id);
        Assert.Contains("creation_id=container_123", handler.LastRequestBody);
        Assert.Contains("access_token=token_abc", handler.LastRequestBody);
    }
}
