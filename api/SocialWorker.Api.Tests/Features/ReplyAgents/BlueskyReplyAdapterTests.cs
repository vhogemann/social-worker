using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.ReplyAgents.Adapters;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class BlueskyReplyAdapterTests
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

    private static (BlueskyReplyAdapter Adapter, MockHttpMessageHandler HttpHandler) CreateAdapter(
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = new MockHttpMessageHandler();
        if (responder != null)
        {
            handler.Handler = responder;
        }

        var client = new HttpClient(handler);
        var apiClient = new BlueskyApiClient(client);
        var facetBuilder = new BlueskyFacetBuilder();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = EncryptionKey
            })
            .Build();

        var adapter = new BlueskyReplyAdapter(apiClient, client, facetBuilder, config, NullLogger<BlueskyReplyAdapter>.Instance);
        return (adapter, handler);
    }

    [Fact]
    public void Platform_ReturnsBluesky()
    {
        var (adapter, _) = CreateAdapter();
        Assert.Equal("Bluesky", adapter.Platform);
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
    public async Task FetchCommentsAsync_ParsesThreadAndFiltersBySince()
    {
        var postUri = "at://did:plc:author123/app.bsky.feed.post/3abc";
        var post = new Post { RemoteId = postUri, RemoteCid = "cid-root" };
        var account = new Account();

        var threadJson = """
        {
          "thread": {
            "$type": "app.bsky.feed.defs#threadViewPost",
            "post": {
              "uri": "at://did:plc:author123/app.bsky.feed.post/3abc",
              "cid": "cid-root",
              "author": { "handle": "author.bsky.social" },
              "record": { "text": "Root post", "createdAt": "2026-10-11T10:00:00Z" }
            },
            "replies": [
              {
                "$type": "app.bsky.feed.defs#threadViewPost",
                "post": {
                  "uri": "at://did:plc:user1/app.bsky.feed.post/reply1",
                  "cid": "cid-reply-1",
                  "author": { "handle": "user1.bsky.social" },
                  "record": { "text": "Recent reply https://example.com", "createdAt": "2026-10-11T12:00:00Z" },
                  "embed": {
                    "$type": "app.bsky.embed.images#view",
                    "images": [
                      { "thumb": "https://cdn.bsky.app/thumb1.jpg", "alt": "A cool chart" }
                    ]
                  }
                },
                "replies": [
                  {
                    "$type": "app.bsky.feed.defs#threadViewPost",
                    "post": {
                      "uri": "at://did:plc:user2/app.bsky.feed.post/reply2",
                      "cid": "cid-reply-2",
                      "author": { "handle": "user2.bsky.social" },
                      "record": { "text": "Older nested reply", "createdAt": "2026-10-11T09:00:00Z" }
                    }
                  }
                ]
              }
            ]
          }
        }
        """;

        var (adapter, _) = CreateAdapter(req =>
        {
            if (req.RequestUri != null && req.RequestUri.ToString().Contains("getPostThread"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(threadJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var since = DateTime.Parse("2026-10-11T11:00:00Z");
        var items = await adapter.FetchCommentsAsync(post, account, since);

        Assert.Single(items);
        var reply1 = items[0];
        Assert.Equal("at://did:plc:user1/app.bsky.feed.post/reply1", reply1.RemoteCommentId);
        Assert.Equal("user1.bsky.social", reply1.RemoteAuthorHandle);
        Assert.Equal("Recent reply https://example.com", reply1.Text);
        Assert.Equal(1, reply1.Depth);
        Assert.Single(reply1.ExtractedUrls);
        Assert.Equal("https://example.com", reply1.ExtractedUrls[0]);
        Assert.Single(reply1.ExtractedImages);
        Assert.Equal("A cool chart", reply1.ExtractedImages[0].AltText);
        var meta = Assert.IsType<BlueskyCommentMetadata>(reply1.PlatformMetadata);
        Assert.Equal("cid-reply-1", meta.Cid);
        Assert.Equal("cid-root", meta.RootCid);
    }

    [Fact]
    public async Task PublishReplyAsync_Success_ReturnsRemoteId()
    {
        var post = new Post { RemoteId = "at://did:plc:root/app.bsky.feed.post/root1", RemoteCid = "cid-root" };
        var account = new Account
        {
            Handle = "bot.bsky.social",
            CredentialsEncrypted = CryptoHelper.EncryptString("my-app-password", EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "Bluesky",
            RemoteCommentId: "at://did:plc:user/app.bsky.feed.post/rep1",
            RemoteParentId: "at://did:plc:root/app.bsky.feed.post/root1",
            RemoteRootId: "at://did:plc:root/app.bsky.feed.post/root1",
            RemoteAuthorHandle: "user.bsky.social",
            RemoteAuthorId: "did:plc:user",
            Text: "Great post!",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
            PlatformMetadata: new BlueskyCommentMetadata("cid-rep1", "cid-root")
        );

        var (adapter, _) = CreateAdapter(req =>
        {
            var url = req.RequestUri?.ToString() ?? "";
            if (url.Contains("createSession"))
            {
                var sessionJson = """{"did":"did:plc:bot123","accessJwt":"jwt-token","refreshJwt":"ref-token","handle":"bot.bsky.social"}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sessionJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (url.Contains("createRecord"))
            {
                var recordJson = """{"uri":"at://did:plc:bot123/app.bsky.feed.post/newreply","cid":"cid-newreply"}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(recordJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var res = await adapter.PublishReplyAsync(post, account, targetComment, "Thanks for following!");

        Assert.True(res.Success);
        Assert.Equal("at://did:plc:bot123/app.bsky.feed.post/newreply", res.RemoteReplyId);
    }

    [Fact]
    public async Task PublishReplyAsync_SessionFailure_ReturnsFailedResult()
    {
        var post = new Post { RemoteId = "at://did:plc:root/app.bsky.feed.post/root1" };
        var account = new Account
        {
            Handle = "bot.bsky.social",
            CredentialsEncrypted = CryptoHelper.EncryptString("wrong-password", EncryptionKey)
        };

        var targetComment = new PlatformCommentItem(
            Platform: "Bluesky",
            RemoteCommentId: "at://did:plc:user/app.bsky.feed.post/rep1",
            RemoteParentId: null,
            RemoteRootId: post.RemoteId,
            RemoteAuthorHandle: "user.bsky.social",
            RemoteAuthorId: "did:plc:user",
            Text: "Hello",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        );

        var (adapter, _) = CreateAdapter(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"AuthenticationRequired","message":"Invalid identifier or password"}""", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var res = await adapter.PublishReplyAsync(post, account, targetComment, "Hi!");

        Assert.False(res.Success);
        Assert.NotNull(res.ErrorMessage);
    }
}
