using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.Twitter;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Twitter;

public sealed class TwitterApiClientTests
{
    private sealed class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task CreateTweetAsync_FirstTweet_SendsCorrectPayloadAndHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            var resp = new TwitterCreateTweetResponse(new TwitterTweetData("tweet_1001", "Hello world!"));
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        });

        var options = new TwitterOptions { BaseUrl = "https://api.twitter.com/2/" };
        var client = new TwitterApiClient(new HttpClient(handler), Options.Create(options), NullLogger<TwitterApiClient>.Instance);

        var result = await client.CreateTweetAsync("my_access_token", "Hello world!");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("my_access_token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Equal("https://api.twitter.com/2/tweets", capturedRequest.RequestUri?.ToString());

        Assert.NotNull(capturedBody);
        var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("Hello world!", doc.RootElement.GetProperty("text").GetString());
        Assert.False(doc.RootElement.TryGetProperty("reply", out _));

        Assert.Equal("tweet_1001", result.Id);
        Assert.Equal("Hello world!", result.Text);
    }

    [Fact]
    public async Task CreateTweetAsync_ReplyTweet_IncludesReplyToId()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            var resp = new TwitterCreateTweetResponse(new TwitterTweetData("tweet_1002", "This is reply 2"));
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        });

        var options = new TwitterOptions { BaseUrl = "https://api.twitter.com/2/" };
        var client = new TwitterApiClient(new HttpClient(handler), Options.Create(options), NullLogger<TwitterApiClient>.Instance);

        var result = await client.CreateTweetAsync("my_access_token", "This is reply 2", inReplyToTweetId: "tweet_1001");

        Assert.NotNull(capturedBody);
        var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("This is reply 2", doc.RootElement.GetProperty("text").GetString());
        Assert.True(doc.RootElement.TryGetProperty("reply", out var replyElem));
        Assert.Equal("tweet_1001", replyElem.GetProperty("in_reply_to_tweet_id").GetString());

        Assert.Equal("tweet_1002", result.Id);
    }

    [Fact]
    public async Task CreateTweetAsync_ApiError_ThrowsInvalidOperationException()
    {
        var handler = new DelegatingHandlerStub(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"detail\":\"The in_reply_to_tweet_id must refer to an existing Tweet.\"}", Encoding.UTF8, "application/json")
            };
        });

        var options = new TwitterOptions { BaseUrl = "https://api.twitter.com/2/" };
        var client = new TwitterApiClient(new HttpClient(handler), Options.Create(options), NullLogger<TwitterApiClient>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateTweetAsync("my_access_token", "Bad reply", inReplyToTweetId: "invalid_id"));

        Assert.Contains("Twitter API returned 400", ex.Message);
        Assert.Contains("in_reply_to_tweet_id must refer to an existing Tweet", ex.Message);
    }

    [Fact]
    public async Task CreateTweetAsync_WithMediaIds_IncludesMediaInPayload()
    {
        string? capturedBody = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var resp = new TwitterCreateTweetResponse(new TwitterTweetData("tweet_1003", "Tweet with pic"));
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        });

        var options = new TwitterOptions { BaseUrl = "https://api.twitter.com/2/" };
        var client = new TwitterApiClient(new HttpClient(handler), Options.Create(options), NullLogger<TwitterApiClient>.Instance);

        var result = await client.CreateTweetAsync("my_access_token", "Tweet with pic", mediaIds: new[] { "media_123", "media_456" });

        Assert.NotNull(capturedBody);
        var doc = JsonDocument.Parse(capturedBody);
        Assert.True(doc.RootElement.TryGetProperty("media", out var mediaElem));
        Assert.True(mediaElem.TryGetProperty("media_ids", out var idsElem));
        Assert.Equal(2, idsElem.GetArrayLength());
        Assert.Equal("media_123", idsElem[0].GetString());
        Assert.Equal("media_456", idsElem[1].GetString());
        Assert.Equal("tweet_1003", result.Id);
    }

    [Fact]
    public async Task UploadMediaAsync_PerformsInitAppendFinalize_ReturnsMediaId()
    {
        int callCount = 0;
        var handler = new DelegatingHandlerStub(req =>
        {
            callCount++;
            if (req.RequestUri!.ToString().EndsWith("/initialize"))
            {
                var resp = new TwitterMediaInitResponse(new TwitterMediaData("media_9999"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri!.ToString().Contains("/media_9999/append"))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (req.RequestUri!.ToString().Contains("/media_9999/finalize"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":{\"id\":\"media_9999\"}}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var options = new TwitterOptions { MediaUploadBaseUrl = "https://api.twitter.com/2/media/upload/" };
        var client = new TwitterApiClient(new HttpClient(handler), Options.Create(options), NullLogger<TwitterApiClient>.Instance);

        var dummyBytes = new byte[] { 1, 2, 3, 4, 5 };
        var mediaId = await client.UploadMediaAsync("my_access_token", dummyBytes, "image/png");

        Assert.Equal("media_9999", mediaId);
        Assert.Equal(3, callCount);
    }
}
