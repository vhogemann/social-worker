using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Twitter;

public sealed class TwitterPublisherTests : SqliteTestBase
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

    private (TwitterPublisher Publisher, MockHttpMessageHandler HttpHandler, AppDbContext Db) CreatePublisher(int interTweetDelayMs = 0)
    {
        var db = CreateDbContext();
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = EncryptionKey
            })
            .Build();

        var twitterOptions = Microsoft.Extensions.Options.Options.Create(new TwitterOptions
        {
            BaseUrl = "https://api.twitter.com/2/",
            InterTweetDelayMs = interTweetDelayMs
        });

        var apiClient = new TwitterApiClient(httpClient, twitterOptions, NullLogger<TwitterApiClient>.Instance);
        var oauth = new TwitterOAuthService(httpClient, twitterOptions, NullLogger<TwitterOAuthService>.Instance);
        var publisher = new TwitterPublisher(
            config,
            twitterOptions,
            db,
            apiClient,
            oauth,
            NullLogger<TwitterPublisher>.Instance);

        return (publisher, handler, db);
    }

    private (AppUser User, Draft Draft, PlatformThread Thread, Account Account) SeedEntities(
        AppDbContext db,
        string threadContent)
    {
        var user = CreateSeedUser(db);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Test Twitter Draft",
            Content = threadContent,
            Status = DraftStatus.Editing
        };
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Twitter",
            Stage = PlatformThreadStage.Draft,
            Content = threadContent
        };
        db.PlatformThreads.Add(thread);

        var credentials = new TwitterAccountCredentials(
            AccessToken: "VALID_ACCESS_TOKEN",
            RefreshToken: "REFRESH_TOKEN",
            TokenType: "bearer");
        var encrypted = CryptoHelper.EncryptString(JsonSerializer.Serialize(credentials), EncryptionKey);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Twitter",
            Handle = "@x_dev",
            ExternalId = "12345678",
            CredentialsEncrypted = encrypted,
            TokenExpiresAt = DateTime.UtcNow.AddHours(2),
            Status = "Active"
        };
        db.Accounts.Add(account);

        db.SaveChanges();
        return (user, draft, thread, account);
    }

    [Fact]
    public async Task PublishAsync_SuccessfulNewThread_ChainsReplies()
    {
        var content = "Segment one text\n\n---\n\nSegment two text";
        var (publisher, handler, db) = CreatePublisher(interTweetDelayMs: 0);
        using (db)
        {
            var (_, _, thread, account) = SeedEntities(db, content);

            int tweetCounter = 100;
            handler.Handler = (req, body) =>
            {
                tweetCounter++;
                var resp = new TwitterCreateTweetResponse(new TwitterTweetData($"tweet_{tweetCounter}", ""));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
                };
            };

            var result = await publisher.PublishAsync(thread, account);

            Assert.True(result.Success);
            Assert.Equal(2, result.Posts.Count);
            Assert.Equal("tweet_101", result.Posts[0].RemoteId);
            Assert.Equal("https://x.com/x_dev/status/tweet_101", result.Posts[0].Url);
            Assert.Equal("tweet_102", result.Posts[1].RemoteId);
            Assert.Equal("https://x.com/x_dev/status/tweet_102", result.Posts[1].Url);

            // Verify chained reply targeting
            var doc1 = JsonDocument.Parse(handler.RequestBodies[0]);
            Assert.Equal("Segment one text", doc1.RootElement.GetProperty("text").GetString());
            Assert.False(doc1.RootElement.TryGetProperty("reply", out _));

            var doc2 = JsonDocument.Parse(handler.RequestBodies[1]);
            Assert.Equal("Segment two text", doc2.RootElement.GetProperty("text").GetString());
            Assert.Equal("tweet_101", doc2.RootElement.GetProperty("reply").GetProperty("in_reply_to_tweet_id").GetString());
        }
    }

    [Fact]
    public async Task PublishAsync_ResumeThread_SkipsPublishedPrefixAndChainsFromLast()
    {
        var content = "Segment one text\n\n---\n\nSegment two text";
        var (publisher, handler, db) = CreatePublisher();
        using (db)
        {
            var (_, draft, thread, account) = SeedEntities(db, content);

            // Seed existing post for segment 0
            var existingPost = new Post
            {
                Id = Guid.NewGuid(),
                DraftId = draft.Id,
                PlatformThreadId = thread.Id,
                Platform = "Twitter",
                SegmentIndex = 0,
                RemoteId = "existing_tweet_100",
                PublishedText = "Segment one text",
                Url = "https://x.com/x_dev/status/existing_tweet_100"
            };
            db.Posts.Add(existingPost);
            db.SaveChanges();

            handler.Handler = (req, body) =>
            {
                var resp = new TwitterCreateTweetResponse(new TwitterTweetData("new_tweet_200", ""));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
                };
            };

            var result = await publisher.PublishAsync(thread, account);

            Assert.True(result.Success);
            Assert.Equal(2, result.Posts.Count);
            Assert.Equal("existing_tweet_100", result.Posts[0].RemoteId);
            Assert.Equal("new_tweet_200", result.Posts[1].RemoteId);

            // Verify only 1 HTTP request made for segment 1, linked to existing_tweet_100
            Assert.Single(handler.RequestBodies);
            var doc = JsonDocument.Parse(handler.RequestBodies[0]);
            Assert.Equal("Segment two text", doc.RootElement.GetProperty("text").GetString());
            Assert.Equal("existing_tweet_100", doc.RootElement.GetProperty("reply").GetProperty("in_reply_to_tweet_id").GetString());
        }
    }

    [Fact]
    public async Task PublishAsync_PartialFailure_ReturnsPartialResult()
    {
        var content = "Segment one text\n\n---\n\nSegment two text";
        var (publisher, handler, db) = CreatePublisher();
        using (db)
        {
            var (_, _, thread, account) = SeedEntities(db, content);

            int callCount = 0;
            handler.Handler = (req, body) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var resp = new TwitterCreateTweetResponse(new TwitterTweetData("tweet_101", ""));
                    return new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"title\":\"Too Many Requests\",\"detail\":\"Rate limit reached\"}", Encoding.UTF8, "application/json")
                };
            };

            var result = await publisher.PublishAsync(thread, account);

            Assert.False(result.Success);
            Assert.Single(result.Posts);
            Assert.Equal("tweet_101", result.Posts[0].RemoteId);
            Assert.Contains("Failed publishing segment 2", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task PublishAsync_UnrecoverablePlan_ReturnsFailed()
    {
        var content = "Segment one text\n\n---\n\nSegment two text";
        var (publisher, _, db) = CreatePublisher();
        using (db)
        {
            var (_, draft, thread, account) = SeedEntities(db, content);

            // Existing post has completely modified text
            var existingPost = new Post
            {
                Id = Guid.NewGuid(),
                DraftId = draft.Id,
                PlatformThreadId = thread.Id,
                Platform = "Twitter",
                SegmentIndex = 0,
                RemoteId = "existing_1",
                PublishedText = "Completely different text",
                Url = "https://x.com/x_dev/status/existing_1"
            };
            db.Posts.Add(existingPost);
            db.SaveChanges();

            var result = await publisher.PublishAsync(thread, account);

            Assert.False(result.Success);
            Assert.Contains("Cannot resume publishing", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task PublishAsync_EmptyThread_ReturnsFailed()
    {
        var (publisher, _, db) = CreatePublisher();
        using (db)
        {
            var (_, _, thread, account) = SeedEntities(db, "");

            var result = await publisher.PublishAsync(thread, account);

            Assert.False(result.Success);
            Assert.Equal("Thread is empty.", result.ErrorMessage);
        }
    }
}
