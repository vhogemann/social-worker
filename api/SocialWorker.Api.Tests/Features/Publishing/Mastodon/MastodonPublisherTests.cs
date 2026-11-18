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
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Mastodon;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Mastodon;

public sealed class MastodonPublisherTests : SqliteTestBase
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();
        public Func<HttpRequestMessage, string, int, HttpResponseMessage> Handler { get; set; } = (_, _, _) => new HttpResponseMessage(HttpStatusCode.OK);
        private int _count;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            Bodies.Add(body);
            var idx = Interlocked.Increment(ref _count);
            return Handler(request, body, idx);
        }
    }

    private (MastodonPublisher Publisher, MockHttpHandler HttpHandler, AppDbContext Db) CreatePublisher()
    {
        var db = CreateDbContext();
        var handler = new MockHttpHandler();
        var apiClient = new MastodonApiClient(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(new MastodonOptions()));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:DbEncryptionKey"] = EncryptionKey })
            .Build();

        var publisher = new MastodonPublisher(config, db, apiClient, NullLogger<MastodonPublisher>.Instance);
        return (publisher, handler, db);
    }

    private static Account CreateAccount(Guid userId)
    {
        var credsJson = JsonSerializer.Serialize(new MastodonCredentials("https://mastodon.social", "tok-abc"));
        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "Mastodon",
            Handle = "@alice@mastodon.social",
            ExternalId = "42",
            CredentialsEncrypted = CryptoHelper.EncryptString(credsJson, EncryptionKey)
        };
    }

    [Fact]
    public async Task PublishAsync_MultiSegmentThread_ChainsRepliesWithPublicRootAndUnlistedReplies()
    {
        var (publisher, handler, db) = CreatePublisher();
        var user = new AppUser { Id = Guid.NewGuid(), Email = "a@b.com", PasswordHash = "h" };
        var account = CreateAccount(user.Id);
        var draft = Draft.Create(user.Id, "Test", targetPlatform: SocialPlatform.Mastodon, targetAccountId: account.Id);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Mastodon",
            Content = "First post\n\n---\n\nSecond post"
        };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Drafts.Add(draft);
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        handler.Handler = (_, _, idx) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"st-{{idx}}","url":"https://mastodon.social/@alice/st-{{idx}}","created_at":"2026-01-01T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Equal(2, result.Posts.Count);
        Assert.Equal("st-1", result.Posts[0].RemoteId);
        Assert.Equal("st-2", result.Posts[1].RemoteId);

        Assert.Contains("\"visibility\":\"public\"", handler.Bodies[0]);
        Assert.DoesNotContain("\"in_reply_to_id\"", handler.Bodies[0]);

        Assert.Contains("\"visibility\":\"unlisted\"", handler.Bodies[1]);
        Assert.Contains("\"in_reply_to_id\":\"st-1\"", handler.Bodies[1]);
    }

    [Fact]
    public async Task PublishAsync_PartialFailureAndResume_SkipsAlreadyPublishedSegments()
    {
        var (publisher, handler, db) = CreatePublisher();
        var user = new AppUser { Id = Guid.NewGuid(), Email = "a@b.com", PasswordHash = "h" };
        var account = CreateAccount(user.Id);
        var draft = Draft.Create(user.Id, "Test", targetPlatform: SocialPlatform.Mastodon, targetAccountId: account.Id);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Mastodon",
            Content = "First post\n\n---\n\nSecond post"
        };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Drafts.Add(draft);
        db.PlatformThreads.Add(thread);
        db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            PlatformThreadId = thread.Id,
            Platform = "Mastodon",
            SegmentIndex = 0,
            RemoteId = "st-existing",
            PublishedText = "First post",
            Url = "https://mastodon.social/@alice/st-existing"
        });
        await db.SaveChangesAsync();

        handler.Handler = (_, _, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"id":"st-resumed","url":"https://mastodon.social/@alice/st-resumed","created_at":"2026-01-01T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Equal(2, result.Posts.Count);
        Assert.Single(handler.Bodies);
        Assert.Contains("\"in_reply_to_id\":\"st-existing\"", handler.Bodies[0]);
        Assert.Contains("\"visibility\":\"unlisted\"", handler.Bodies[0]);
    }
}
