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
using SocialWorker.Api.Features.Publishing.Mastodon;
using SocialWorker.Api.Features.ReplyAgents.Adapters;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class MastodonReplyAdapterTests
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return Handler(request, body);
        }
    }

    private static (MastodonReplyAdapter Adapter, MockHttpHandler Handler) CreateAdapter()
    {
        var handler = new MockHttpHandler();
        var apiClient = new MastodonApiClient(new HttpClient(handler), Options.Create(new MastodonOptions()));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:DbEncryptionKey"] = EncryptionKey })
            .Build();
        return (new MastodonReplyAdapter(apiClient, config, NullLogger<MastodonReplyAdapter>.Instance), handler);
    }

    private static Account CreateAccount()
    {
        var credsJson = JsonSerializer.Serialize(new MastodonCredentials("https://mastodon.social", "tok-abc"));
        return new Account
        {
            Id = Guid.NewGuid(),
            Platform = "Mastodon",
            Handle = "@me@mastodon.social",
            ExternalId = "1",
            CredentialsEncrypted = CryptoHelper.EncryptString(credsJson, EncryptionKey)
        };
    }

    [Fact]
    public async Task FetchCommentsAsync_StripsHtmlAndSkipsDirectMessages()
    {
        var (adapter, handler) = CreateAdapter();
        handler.Handler = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "ancestors": [],
                  "descendants": [
                    {
                      "id": "c-1",
                      "content": "<p>Great post! <a href=\"https://example.com\">link</a></p>",
                      "created_at": "2026-01-02T00:00:00Z",
                      "in_reply_to_id": "post-1",
                      "visibility": "public",
                      "account": { "id": "u-1", "username": "bob", "acct": "bob@fosstodon.org" }
                    },
                    {
                      "id": "c-2",
                      "content": "<p>Private DM</p>",
                      "created_at": "2026-01-02T00:01:00Z",
                      "in_reply_to_id": "post-1",
                      "visibility": "direct",
                      "account": { "id": "u-2", "username": "carol", "acct": "carol" }
                    }
                  ]
                }
                """, Encoding.UTF8, "application/json")
        };

        var post = new Post { Id = Guid.NewGuid(), RemoteId = "post-1", Platform = "Mastodon" };
        var comments = await adapter.FetchCommentsAsync(post, CreateAccount(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(comments);
        Assert.Equal("c-1", comments[0].RemoteCommentId);
        Assert.Equal("Great post! link", comments[0].Text);
        Assert.Equal("bob@fosstodon.org", comments[0].RemoteAuthorHandle);
    }

    [Fact]
    public async Task PublishReplyAndLike_CallsStatusesAndFavouriteEndpoints()
    {
        var (adapter, handler) = CreateAdapter();
        handler.Handler = (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/v1/statuses")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"reply-1","url":"https://mastodon.social/@me/reply-1","created_at":"2026-01-02T00:00:00Z"}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"c-1","created_at":"2026-01-02T00:00:00Z","favourited":true}""", Encoding.UTF8, "application/json")
            };
        };

        var post = new Post { Id = Guid.NewGuid(), RemoteId = "post-1", Platform = "Mastodon" };
        var comment = new PlatformCommentItem(
            Platform: "Mastodon",
            RemoteCommentId: "c-1",
            RemoteParentId: "post-1",
            RemoteRootId: "post-1",
            RemoteAuthorHandle: "bob@fosstodon.org",
            RemoteAuthorId: "u-1",
            Text: "Hello",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
            PlatformMetadata: new Dictionary<string, string> { ["visibility"] = "unlisted" });

        var account = CreateAccount();
        var replyRes = await adapter.PublishReplyAsync(post, account, comment, "Thanks!");
        Assert.True(replyRes.Success);
        Assert.Equal("reply-1", replyRes.RemoteReplyId);
        Assert.Contains("@bob@fosstodon.org Thanks!", handler.Requests[0].Body);
        Assert.Contains("\"visibility\":\"unlisted\"", handler.Requests[0].Body);

        var likeRes = await adapter.LikeCommentAsync(post, account, comment);
        Assert.True(likeRes.Success);
        Assert.Equal("/api/v1/statuses/c-1/favourite", handler.Requests[1].Path);
    }
}
