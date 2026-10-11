using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Features.ReplyAgents.Adapters;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class MetaThreadsReplyAdapterTests
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(Handler(request));
        }
    }

    private static (MetaThreadsReplyAdapter Adapter, MockHttpMessageHandler HttpHandler) CreateAdapter(
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = new MockHttpMessageHandler();
        if (responder != null)
        {
            handler.Handler = responder;
        }

        var client = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new MetaThreadsOptions
        {
            BaseUrl = "https://graph.threads.net/v1.0"
        });
        var apiClient = new MetaThreadsApiClient(client, options, NullLogger<MetaThreadsApiClient>.Instance);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = EncryptionKey
            })
            .Build();

        var adapter = new MetaThreadsReplyAdapter(apiClient, config, NullLogger<MetaThreadsReplyAdapter>.Instance);
        return (adapter, handler);
    }

    [Fact]
    public void Platform_ReturnsMetaThreads()
    {
        var (adapter, _) = CreateAdapter();
        Assert.Equal("MetaThreads", adapter.Platform);
    }

    [Fact]
    public async Task FetchCommentsAsync_EmptyRemoteId_ReturnsEmpty()
    {
        var (adapter, _) = CreateAdapter();
        var post = new Post { RemoteId = "" };
        var account = new Account();

        var results = await adapter.FetchCommentsAsync(post, account, DateTime.UtcNow.AddHours(-1));
        Assert.Empty(results);
    }

    [Fact]
    public async Task FetchCommentsAsync_ParsesRepliesAndFiltersBySince()
    {
        var post = new Post { RemoteId = "1001" };
        var account = new Account
        {
            ExternalId = "999",
            CredentialsEncrypted = CryptoHelper.EncryptString("test-token", EncryptionKey)
        };

        var repliesJson = """
        {
          "data": [
            {
              "id": "reply_2001",
              "text": "Check this article: https://example.org/news",
              "username": "threads_fan",
              "timestamp": "2026-10-11T12:00:00+0000",
              "replied_to": { "id": "1001" }
            },
            {
              "id": "reply_2002",
              "text": "Older reply",
              "username": "old_user",
              "timestamp": "2026-10-11T08:00:00+0000"
            }
          ]
        }
        """;

        var (adapter, _) = CreateAdapter(req =>
        {
            if (req.RequestUri != null && (req.RequestUri.ToString().Contains("/conversation") || req.RequestUri.ToString().Contains("/replies")))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(repliesJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var since = DateTime.Parse("2026-10-11T10:00:00Z");
        var items = await adapter.FetchCommentsAsync(post, account, since);

        Assert.Single(items);
        var item = items[0];
        Assert.Equal("reply_2001", item.RemoteCommentId);
        Assert.Equal("threads_fan", item.RemoteAuthorHandle);
        Assert.Equal("Check this article: https://example.org/news", item.Text);
        Assert.Equal(1, item.Depth);
        Assert.Single(item.ExtractedUrls);
        Assert.Equal("https://example.org/news", item.ExtractedUrls[0]);
    }

    [Fact]
    public async Task FetchCommentsAsync_CalculatesCorrectDepthForNestedReplies()
    {
        var post = new Post { RemoteId = "1001" };
        var account = new Account
        {
            ExternalId = "999",
            CredentialsEncrypted = CryptoHelper.EncryptString("test-token", EncryptionKey)
        };

        var conversationJson = """
        {
          "data": [
            {
              "id": "reply_1",
              "text": "First comment",
              "username": "user1",
              "timestamp": "2026-10-11T12:00:00+0000",
              "replied_to": { "id": "1001" }
            },
            {
              "id": "reply_2",
              "text": "Agent reply to user1",
              "username": "vhogemann",
              "timestamp": "2026-10-11T12:05:00+0000",
              "replied_to": { "id": "reply_1" }
            },
            {
              "id": "reply_3",
              "text": "User1 counter-reply to agent",
              "username": "user1",
              "timestamp": "2026-10-11T12:10:00+0000",
              "replied_to": { "id": "reply_2" }
            }
          ]
        }
        """;

        var (adapter, _) = CreateAdapter(req =>
        {
            if (req.RequestUri != null && req.RequestUri.ToString().Contains("/conversation"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(conversationJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var since = DateTime.Parse("2026-10-11T11:00:00Z");
        var items = await adapter.FetchCommentsAsync(post, account, since);

        Assert.Equal(3, items.Count);
        var r1 = items.First(i => i.RemoteCommentId == "reply_1");
        var r2 = items.First(i => i.RemoteCommentId == "reply_2");
        var r3 = items.First(i => i.RemoteCommentId == "reply_3");

        Assert.Equal(1, r1.Depth);
        Assert.Equal(2, r2.Depth);
        Assert.Equal(3, r3.Depth);
        Assert.Equal("reply_2", r3.RemoteParentId);
    }

    [Fact]
    public async Task PublishReplyAsync_Success_ReturnsPublishedId()
    {
        var post = new Post { RemoteId = "1001" };
        var account = new Account
        {
            ExternalId = "999",
            CredentialsEncrypted = CryptoHelper.EncryptString("test-token", EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "MetaThreads",
            RemoteCommentId: "reply_2001",
            RemoteParentId: "1001",
            RemoteRootId: "1001",
            RemoteAuthorHandle: "threads_fan",
            RemoteAuthorId: "threads_fan",
            Text: "Great post!",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        );

        var (adapter, _) = CreateAdapter(req =>
        {
            var url = req.RequestUri?.ToString() ?? "";
            if (url.Contains("/threads_publish") && req.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"published_4001"}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (url.Contains("/threads") && req.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"container_3001"}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (req.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"container_3001","status":"FINISHED"}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var res = await adapter.PublishReplyAsync(post, account, targetComment, "Thanks for reading!");

        Assert.True(res.Success);
        Assert.Equal("published_4001", res.RemoteReplyId);
    }

    [Fact]
    public async Task PublishReplyAsync_ContainerCreationFails_ReturnsFailure()
    {
        var post = new Post { RemoteId = "1001" };
        var account = new Account
        {
            ExternalId = "999",
            CredentialsEncrypted = CryptoHelper.EncryptString("test-token", EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "MetaThreads",
            RemoteCommentId: "reply_2001",
            RemoteParentId: "1001",
            RemoteRootId: "1001",
            RemoteAuthorHandle: "threads_fan",
            RemoteAuthorId: "threads_fan",
            Text: "Great post!",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        );

        var (adapter, _) = CreateAdapter(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":{"message":"Invalid user parameter","code":100}}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var res = await adapter.PublishReplyAsync(post, account, targetComment, "Thanks!");

        Assert.False(res.Success);
        Assert.NotNull(res.ErrorMessage);
    }
}
