using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Features.ReplyAgents.Adapters;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class TwitterReplyAdapterTests
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> RequestBodies { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
            RequestBodies.Add(body);
            return Handler(request, body);
        }
    }

    private (TwitterReplyAdapter Adapter, MockHttpMessageHandler HttpHandler) CreateAdapter()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = EncryptionKey
            })
            .Build();

        var twitterOptions = Options.Create(new TwitterOptions
        {
            BaseUrl = "https://api.twitter.com/2/"
        });

        var apiClient = new TwitterApiClient(httpClient, twitterOptions, NullLogger<TwitterApiClient>.Instance);
        var oauth = new TwitterOAuthService(httpClient, twitterOptions, NullLogger<TwitterOAuthService>.Instance);

        var adapter = new TwitterReplyAdapter(
            apiClient,
            oauth,
            config,
            NullLogger<TwitterReplyAdapter>.Instance);

        return (adapter, handler);
    }

    [Fact]
    public void Platform_ReturnsTwitter()
    {
        var (adapter, _) = CreateAdapter();
        Assert.Equal("Twitter", adapter.Platform);
    }

    [Fact]
    public async Task FetchCommentsAsync_FetchesAndMapsCommentsFromSearch()
    {
        var (adapter, httpHandler) = CreateAdapter();

        httpHandler.Handler = (req, body) =>
        {
            var resp = new TwitterSearchTweetsResponse(
                Data: new List<TwitterSearchTweetItem>
                {
                    new("1001", "Root tweet text", "author_me", DateTime.UtcNow.AddMinutes(-10), null, "1001"),
                    new("2001", "This is an opponent reply with https://example.com/link", "author_opp", DateTime.UtcNow.AddMinutes(-5), "author_me", "1001")
                },
                Includes: new TwitterSearchIncludes(
                    Users: new List<TwitterUserData>
                    {
                        new("author_me", "Tester", "tester"),
                        new("author_opp", "Rival", "rival_debater")
                    }
                )
            );
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        };

        var post = new Post { RemoteId = "1001", Platform = "Twitter" };
        var credentials = new TwitterAccountCredentials("valid_bearer_token");
        var account = new Account
        {
            Platform = "Twitter",
            Handle = "tester",
            CredentialsEncrypted = CryptoHelper.EncryptString(JsonSerializer.Serialize(credentials), EncryptionKey)
        };

        var comments = await adapter.FetchCommentsAsync(post, account, DateTime.UtcNow.AddHours(-1));

        Assert.Single(comments);
        var comment = comments[0];
        Assert.Equal("2001", comment.RemoteCommentId);
        Assert.Equal("rival_debater", comment.RemoteAuthorHandle);
        Assert.Equal("author_opp", comment.RemoteAuthorId);
        Assert.Contains("https://example.com/link", comment.ExtractedUrls);
        Assert.Equal("1001", comment.RemoteParentId);
    }

    [Fact]
    public async Task PublishReplyAsync_SendsTweetChainedToTargetComment()
    {
        var (adapter, httpHandler) = CreateAdapter();

        httpHandler.Handler = (req, body) =>
        {
            var resp = new TwitterCreateTweetResponse(new TwitterTweetData("reply_tweet_99", "This is a factual rebuttal."));
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        };

        var post = new Post { RemoteId = "tweet_root_1", Platform = "Twitter" };
        var credentials = new TwitterAccountCredentials("valid_bearer_token");
        var account = new Account
        {
            Platform = "Twitter",
            Handle = "@rebuttal_bot",
            CredentialsEncrypted = CryptoHelper.EncryptString(JsonSerializer.Serialize(credentials), EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "Twitter",
            RemoteCommentId: "comment_target_42",
            RemoteParentId: "tweet_root_1",
            RemoteRootId: "tweet_root_1",
            RemoteAuthorHandle: "opponent",
            RemoteAuthorId: "opponent_id",
            Text: "You are wrong about economics!",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
            PlatformMetadata: null);

        var result = await adapter.PublishReplyAsync(post, account, targetComment, "This is a factual rebuttal.");

        Assert.True(result.Success);
        Assert.Equal("reply_tweet_99", result.RemoteReplyId);
        Assert.Equal("https://x.com/rebuttal_bot/status/reply_tweet_99", result.RemoteReplyUrl);

        Assert.Single(httpHandler.Requests);
        var requestBody = httpHandler.RequestBodies[0];
        var doc = JsonDocument.Parse(requestBody);
        Assert.Equal("This is a factual rebuttal.", doc.RootElement.GetProperty("text").GetString());
        Assert.Equal("comment_target_42", doc.RootElement.GetProperty("reply").GetProperty("in_reply_to_tweet_id").GetString());
    }

    [Fact]
    public async Task PublishReplyAsync_WhenApiFails_ReturnsFailureResult()
    {
        var (adapter, httpHandler) = CreateAdapter();

        httpHandler.Handler = (req, body) =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"detail\":\"The in_reply_to_tweet_id was not found.\"}", Encoding.UTF8, "application/json")
            };
        };

        var post = new Post { RemoteId = "tweet_root_1", Platform = "Twitter" };
        var credentials = new TwitterAccountCredentials("valid_bearer_token");
        var account = new Account
        {
            Platform = "Twitter",
            Handle = "bot",
            CredentialsEncrypted = CryptoHelper.EncryptString(JsonSerializer.Serialize(credentials), EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "Twitter",
            RemoteCommentId: "missing_id",
            RemoteParentId: null,
            RemoteRootId: "tweet_root_1",
            RemoteAuthorHandle: "user",
            RemoteAuthorId: "user",
            Text: "hello",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
            PlatformMetadata: null);

        var result = await adapter.PublishReplyAsync(post, account, targetComment, "reply text");

        Assert.False(result.Success);
        Assert.Contains("Twitter API returned 400", result.ErrorMessage);
    }

    [Fact]
    public async Task LikeCommentAsync_ValidAccount_CallsLikesEndpointAndReturnsSuccess()
    {
        var (adapter, httpHandler) = CreateAdapter();

        httpHandler.Handler = (req, body) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("users/user_123/likes"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":{"liked":true}}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var post = new Post { RemoteId = "tweet_root_1", Platform = "Twitter" };
        var credentials = new TwitterAccountCredentials("valid_bearer_token");
        var account = new Account
        {
            Platform = "Twitter",
            Handle = "bot",
            ExternalId = "user_123",
            CredentialsEncrypted = CryptoHelper.EncryptString(JsonSerializer.Serialize(credentials), EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "Twitter",
            RemoteCommentId: "tweet_comment_456",
            RemoteParentId: null,
            RemoteRootId: "tweet_root_1",
            RemoteAuthorHandle: "fan",
            RemoteAuthorId: "fan_id",
            Text: "Great thread!",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
            PlatformMetadata: null);

        var result = await adapter.LikeCommentAsync(post, account, targetComment);

        Assert.True(result.Success);
        Assert.Equal("twitter-like-user_123-tweet_comment_456", result.RemoteLikeId);
    }
}
