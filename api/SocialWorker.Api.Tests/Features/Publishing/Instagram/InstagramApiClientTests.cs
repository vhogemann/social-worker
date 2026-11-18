using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Features.Publishing.Instagram;
using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Instagram;

public sealed class InstagramApiClientTests
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

    private static InstagramApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> factory, InstagramOptions? options = null)
    {
        var handler = new FakeHttpMessageHandler(factory);
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? new InstagramOptions());
        return new InstagramApiClient(httpClient, opts, NullLogger<InstagramApiClient>.Instance);
    }

    [Fact]
    public async Task CreateImageContainerAsync_InstagramLogin_UsesGraphInstagramUrlAndSendsParameters()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"container_123\"}")
        });
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(new InstagramOptions
        {
            OAuthBaseUrl = "https://api.instagram.com/oauth/authorize",
            InstagramGraphBaseUrl = "https://graph.instagram.com/"
        });
        var client = new InstagramApiClient(httpClient, opts, NullLogger<InstagramApiClient>.Instance);

        var res = await client.CreateImageContainerAsync(
            "ig_user_1",
            "tok_123",
            "https://app.test.com/api/media/abc",
            caption: "Hello Instagram",
            altText: "A sunset",
            isCarouselItem: false);

        Assert.Equal("container_123", res.Id);
        Assert.Equal("https://graph.instagram.com/v21.0/ig_user_1/media", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("image_url=https%3A%2F%2Fapp.test.com%2Fapi%2Fmedia%2Fabc", handler.LastRequestBody);
        Assert.Contains("caption=Hello+Instagram", handler.LastRequestBody);
        Assert.Contains("alt_text=A+sunset", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateImageContainerAsync_FacebookLogin_UsesGraphFacebookBaseUrl()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"container_fb\"}")
        });
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(new InstagramOptions
        {
            OAuthBaseUrl = "https://www.facebook.com/v20.0/dialog/oauth",
            BaseUrl = "https://graph.facebook.com/v20.0/"
        });
        var client = new InstagramApiClient(httpClient, opts, NullLogger<InstagramApiClient>.Instance);

        var res = await client.CreateImageContainerAsync(
            "ig_user_2",
            "tok_fb",
            "https://app.test.com/api/media/def",
            isCarouselItem: true);

        Assert.Equal("container_fb", res.Id);
        Assert.Equal("https://graph.facebook.com/v20.0/ig_user_2/media", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("is_carousel_item=true", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateCarouselContainerAsync_WithLessThanTwoChildren_ThrowsArgumentException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreateCarouselContainerAsync("ig_user_1", "tok", new[] { "child_1" }, "Caption"));
    }

    [Fact]
    public async Task WaitForContainerReadyAsync_PollsUntilFinishedWithExponentialBackoff()
    {
        int calls = 0;
        var delays = new List<TimeSpan>();
        var client = CreateClient(_ =>
        {
            calls++;
            var statusCode = calls < 3 ? "IN_PROGRESS" : "FINISHED";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"id\":\"c1\",\"status_code\":\"{statusCode}\"}}")
            };
        });

        var ready = await client.WaitForContainerReadyAsync(
            "c1",
            "tok",
            timeout: TimeSpan.FromSeconds(30),
            initialDelay: TimeSpan.FromMilliseconds(10),
            delayFunc: (d, _) =>
            {
                delays.Add(d);
                return Task.CompletedTask;
            });

        Assert.True(ready);
        Assert.Equal(3, calls);
        Assert.Equal(2, delays.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(10), delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(20), delays[1]);
    }

    [Fact]
    public async Task WaitForContainerReadyAsync_OnErrorStatus_ThrowsInstagramApiException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"c1\",\"status_code\":\"ERROR\",\"status\":\"Invalid image format\"}")
        });

        var ex = await Assert.ThrowsAsync<InstagramApiException>(() =>
            client.WaitForContainerReadyAsync("c1", "tok"));

        Assert.Contains("Invalid image format", ex.Message);
    }

    [Fact]
    public async Task PublishContainerAsync_PostsToMediaPublishEndpoint()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"ig_media_999\"}")
        });
        var httpClient = new HttpClient(handler);
        var client = new InstagramApiClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new InstagramOptions()),
            NullLogger<InstagramApiClient>.Instance);

        var res = await client.PublishContainerAsync("ig_user_1", "tok", "c1");

        Assert.Equal("ig_media_999", res.Id);
        Assert.Equal("https://graph.instagram.com/v21.0/ig_user_1/media_publish", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("creation_id=c1", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetMediaPermalinkAsync_ReturnsPermalinkWhenPresent()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"ig_media_999\",\"permalink\":\"https://www.instagram.com/p/Cxyz123/\",\"shortcode\":\"Cxyz123\"}")
        });

        var res = await client.GetMediaPermalinkAsync("ig_media_999", "tok");

        Assert.NotNull(res);
        Assert.Equal("https://www.instagram.com/p/Cxyz123/", res!.Permalink);
        Assert.Equal("Cxyz123", res.Shortcode);
    }

    [Fact]
    public async Task CreateImageContainerAsync_ApiError_ThrowsInstagramApiExceptionWithDetails()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Invalid parameter\",\"type\":\"OAuthException\",\"code\":100}}")
        });

        var ex = await Assert.ThrowsAsync<InstagramApiException>(() =>
            client.CreateImageContainerAsync("ig_user_1", "tok", "https://example.com/a.jpg"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(100, ex.ErrorCode);
        Assert.Equal("OAuthException", ex.ErrorType);
        Assert.Equal("Invalid parameter", ex.Message);
    }
}
