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
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.GitHubBlog;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.GitHubBlog;

public sealed class GitHubBlogPublisherTests : SqliteTestBase
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            Requests.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty, body));
            return Handler(request, body);
        }
    }

    private (GitHubBlogPublisher Publisher, MockHttpHandler HttpHandler, AppDbContext Db) CreatePublisher()
    {
        var db = CreateDbContext();
        var handler = new MockHttpHandler();
        var apiClient = new GitHubApiClient(new HttpClient(handler));
        var docBuilder = new GitHubBlogDocumentBuilder();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:DbEncryptionKey"] = EncryptionKey })
            .Build();

        var publisher = new GitHubBlogPublisher(config, db, apiClient, docBuilder, NullLogger<GitHubBlogPublisher>.Instance);
        return (publisher, handler, db);
    }

    private static Account CreateAccount(Guid userId)
    {
        var credsJson = JsonSerializer.Serialize(new GitHubBlogCredentials(
            Owner: "alice",
            Repo: "alice.github.io",
            Branch: "main",
            AccessToken: "ghp_tok",
            SiteBaseUrl: "https://alice.github.io"));

        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "GitHubBlog",
            Handle = "alice/alice.github.io",
            ExternalId = "999",
            CredentialsEncrypted = CryptoHelper.EncryptString(credsJson, EncryptionKey)
        };
    }

    [Fact]
    public async Task PublishAsync_CommitsArticleAndAppendsSuffixOnPathCollision()
    {
        var (publisher, handler, db) = CreatePublisher();
        var user = new AppUser { Id = Guid.NewGuid(), Email = "a@b.com", PasswordHash = "h" };
        var account = CreateAccount(user.Id);
        var draft = Draft.Create(user.Id, "My Post", targetPlatform: SocialPlatform.GitHubBlog, targetAccountId: account.Id);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "GitHubBlog",
            Content = "# My Post\n\nLong form content here."
        };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Drafts.Add(draft);
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        int existsCalls = 0;
        handler.Handler = (req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && url.Contains("/contents/"))
            {
                var call = Interlocked.Increment(ref existsCalls);
                return new HttpResponseMessage(call == 1 ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            }
            if (req.Method == HttpMethod.Get && url.EndsWith("/git/ref/heads/main"))
            {
                return JsonOk("""{"ref":"refs/heads/main","object":{"sha":"head-1","type":"commit"}}""");
            }
            if (req.Method == HttpMethod.Get && url.EndsWith("/git/commits/head-1"))
            {
                return JsonOk("""{"sha":"head-1","tree":{"sha":"tree-0"}}""");
            }
            if (req.Method == HttpMethod.Post && url.EndsWith("/git/blobs"))
            {
                return JsonOk("""{"sha":"blob-1"}""");
            }
            if (req.Method == HttpMethod.Post && url.EndsWith("/git/trees"))
            {
                return JsonOk("""{"sha":"tree-1"}""");
            }
            if (req.Method == HttpMethod.Post && url.EndsWith("/git/commits"))
            {
                return JsonOk("""{"sha":"commit-abc"}""");
            }
            if (req.Method == HttpMethod.Patch && url.EndsWith("/git/refs/heads/main"))
            {
                return JsonOk("""{"ref":"refs/heads/main","object":{"sha":"commit-abc"}}""");
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        };

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("commit-abc", result.Posts[0].RemoteId);
        Assert.EndsWith("-my-post-2.md", result.Posts[0].RemoteCid);
        Assert.EndsWith("/my-post-2/", result.Posts[0].Url);
        Assert.Equal(2, existsCalls);
    }

    [Fact]
    public async Task PublishAsync_IsIdempotentWhenPostAlreadyRecorded()
    {
        var (publisher, handler, db) = CreatePublisher();
        var user = new AppUser { Id = Guid.NewGuid(), Email = "a@b.com", PasswordHash = "h" };
        var account = CreateAccount(user.Id);
        var draft = Draft.Create(user.Id, "My Post", targetPlatform: SocialPlatform.GitHubBlog, targetAccountId: account.Id);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "GitHubBlog",
            Content = "# My Post\n\nBody"
        };
        var existingPost = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            Platform = "GitHubBlog",
            SegmentIndex = 0,
            RemoteId = "existing-sha",
            RemoteCid = "_posts/2026-11-19-my-post.md",
            Url = "https://alice.github.io/2026/11/19/my-post/"
        };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Drafts.Add(draft);
        db.PlatformThreads.Add(thread);
        db.Posts.Add(existingPost);
        await db.SaveChangesAsync();

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("existing-sha", result.Posts[0].RemoteId);
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
