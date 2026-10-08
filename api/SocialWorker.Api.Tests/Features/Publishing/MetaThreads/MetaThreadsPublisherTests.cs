using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.MetaThreads;

public sealed class MetaThreadsPublisherTests : SqliteTestBase
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

    private (MetaThreadsPublisher Publisher, MockHttpMessageHandler HttpHandler, AppDbContext Db) CreatePublisher(
        string? publicBaseUrl = null)
    {
        var db = CreateDbContext();
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = EncryptionKey,
                ["App:BaseUrl"] = publicBaseUrl ?? "https://app.test.com"
            })
            .Build();

        var threadsOptions = Microsoft.Extensions.Options.Options.Create(new MetaThreadsOptions
        {
            BaseUrl = "https://graph.threads.net/v1.0",
            PublicBaseUrl = publicBaseUrl
        });

        var apiClient = new MetaThreadsApiClient(httpClient, threadsOptions, NullLogger<MetaThreadsApiClient>.Instance);
        var publisher = new MetaThreadsPublisher(config, threadsOptions, db, apiClient, NullLogger<MetaThreadsPublisher>.Instance);

        return (publisher, handler, db);
    }

    [Fact]
    public async Task PublishAsync_NewThread_PublishesAllSegmentsWithChainedReplies()
    {
        var (publisher, httpHandler, db) = CreatePublisher();

        int containerSeq = 0;
        int postSeq = 0;
        httpHandler.Handler = (req, body) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/threads"))
            {
                containerSeq++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"id\":\"container_{containerSeq}\"}}")
                };
            }
            if (req.RequestUri.AbsolutePath.EndsWith("/threads_publish"))
            {
                postSeq++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"id\":\"post_{postSeq}\"}}")
                };
            }
            // status check
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"c\",\"status\":\"FINISHED\"}")
            };
        };

        var user = CreateSeedUser(db);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "techlead",
            ExternalId = "threads_user_123",
            CredentialsEncrypted = CryptoHelper.EncryptString("access_token_xyz", EncryptionKey)
        };
        db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id
        };
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Content = "First segment of thread.\n\n---\n\nSecond segment replying to first."
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.False(result.IsPartial);
        Assert.Equal(2, result.Posts.Count);
        Assert.Equal("post_1", result.Posts[0].RemoteId);
        Assert.Equal("post_2", result.Posts[1].RemoteId);
        Assert.Equal("https://www.threads.net/@techlead/post/post_1", result.Posts[0].Url);
        Assert.Equal("https://www.threads.net/@techlead/post/post_2", result.Posts[1].Url);

        var secondContainerReq = httpHandler.RequestBodies.Find(b => b.Contains("Second+segment"));
        Assert.NotNull(secondContainerReq);
        Assert.Contains("reply_to_id=post_1", secondContainerReq);
    }

    [Fact]
    public async Task PublishAsync_ResumeExisting_SkipsAlreadyPublishedAndLinksToLastPost()
    {
        var (publisher, httpHandler, db) = CreatePublisher();

        httpHandler.Handler = (req, body) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/threads"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"container_resume\"}") };
            }
            if (req.RequestUri.AbsolutePath.EndsWith("/threads_publish"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"post_resumed_2\"}") };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"c\",\"status\":\"FINISHED\"}") };
        };

        var user = CreateSeedUser(db);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "techlead",
            ExternalId = "threads_user_123",
            CredentialsEncrypted = CryptoHelper.EncryptString("token", EncryptionKey)
        };
        db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id
        };
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Content = "Segment 1 already published.\n\n---\n\nSegment 2 waiting to be published."
        };
        db.PlatformThreads.Add(thread);

        db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            SegmentIndex = 0,
            RemoteId = "post_existing_1",
            PublishedText = "Segment 1 already published.",
            Url = "https://www.threads.net/@techlead/post/post_existing_1"
        });
        await db.SaveChangesAsync();

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Equal(2, result.Posts.Count);
        Assert.Equal("post_existing_1", result.Posts[0].RemoteId);
        Assert.Equal("post_resumed_2", result.Posts[1].RemoteId);

        var containerBody = httpHandler.RequestBodies.Find(b => b.Contains("Segment+2"));
        Assert.NotNull(containerBody);
        Assert.Contains("reply_to_id=post_existing_1", containerBody);
    }

    [Fact]
    public async Task PublishAsync_PrefixMismatch_ReturnsFailedWithoutPublishing()
    {
        var (publisher, httpHandler, db) = CreatePublisher();

        var user = CreateSeedUser(db);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "techlead",
            ExternalId = "threads_user_123",
            CredentialsEncrypted = CryptoHelper.EncryptString("token", EncryptionKey)
        };
        db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id
        };
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Content = "MODIFIED Segment 1 text.\n\n---\n\nSegment 2 text."
        };
        db.PlatformThreads.Add(thread);

        db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            SegmentIndex = 0,
            RemoteId = "post_1",
            PublishedText = "ORIGINAL Segment 1 text.",
            Url = "https://www.threads.net/@techlead/post/post_1"
        });
        await db.SaveChangesAsync();

        var result = await publisher.PublishAsync(thread, account);

        Assert.False(result.Success);
        Assert.Contains("was previously published with different content", result.ErrorMessage);
        Assert.Empty(httpHandler.Requests);
    }

    [Fact]
    public async Task PublishAsync_WhenSecondSegmentFails_ReturnsPartialResult()
    {
        var (publisher, httpHandler, db) = CreatePublisher();

        int callCount = 0;
        httpHandler.Handler = (req, body) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/threads"))
            {
                callCount++;
                if (callCount > 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("{\"error\":{\"message\":\"Rate limit reached\"}}")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"container_1\"}") };
            }
            if (req.RequestUri.AbsolutePath.EndsWith("/threads_publish"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"post_1\"}") };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"c\",\"status\":\"FINISHED\"}") };
        };

        var user = CreateSeedUser(db);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "techlead",
            ExternalId = "threads_user_123",
            CredentialsEncrypted = CryptoHelper.EncryptString("token", EncryptionKey)
        };
        db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id
        };
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Content = "Segment 1 text.\n\n---\n\nSegment 2 text."
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var result = await publisher.PublishAsync(thread, account);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        Assert.Single(result.Posts);
        Assert.Equal("post_1", result.Posts[0].RemoteId);
        Assert.Contains("Rate limit reached", result.ErrorMessage);
    }
}
