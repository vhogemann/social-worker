using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Publishing.GitHubBlog;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.GitHubBlog;

public sealed class GitHubApiClientTests
{
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string Body, string? Auth, string? ApiVersion)> Requests { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            var apiVer = request.Headers.TryGetValues("X-GitHub-Api-Version", out var vals) ? string.Join(",", vals) : null;
            Requests.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty, body, request.Headers.Authorization?.ToString(), apiVer));
            return Handler(request, body);
        }
    }

    [Fact]
    public async Task VerifyRepositoryAsync_SucceedsWithPushPermissions()
    {
        var handler = new MockHttpHandler
        {
            Handler = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":12345,"full_name":"alice/alice.github.io","default_branch":"main","permissions":{"push":true,"admin":false,"pull":true}}""", Encoding.UTF8, "application/json")
            }
        };

        var client = new GitHubApiClient(new HttpClient(handler));
        var result = await client.VerifyRepositoryAsync("alice", "alice.github.io", "ghp_secret");

        Assert.True(result.Success);
        Assert.Equal(12345, result.Data!.Id);
        Assert.Equal("main", result.Data.DefaultBranch);
        Assert.Single(handler.Requests);
        Assert.Equal("Bearer ghp_secret", handler.Requests[0].Auth);
        Assert.Equal("2022-11-28", handler.Requests[0].ApiVersion);
    }

    [Fact]
    public async Task VerifyRepositoryAsync_FailsWhenPushPermissionMissing()
    {
        var handler = new MockHttpHandler
        {
            Handler = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":12345,"full_name":"alice/alice.github.io","default_branch":"main","permissions":{"push":false,"admin":false,"pull":true}}""", Encoding.UTF8, "application/json")
            }
        };

        var client = new GitHubApiClient(new HttpClient(handler));
        var result = await client.VerifyRepositoryAsync("alice", "alice.github.io", "ghp_readonly");

        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Contains("push permission", result.Error);
    }

    [Fact]
    public async Task FileExistsAsync_DistinguishesFoundAndNotFound()
    {
        var handler = new MockHttpHandler
        {
            Handler = (req, _) => req.RequestUri!.ToString().Contains("exists.md")
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var client = new GitHubApiClient(new HttpClient(handler));

        var exists = await client.FileExistsAsync("alice", "blog", "main", "_posts/exists.md", "tok");
        var missing = await client.FileExistsAsync("alice", "blog", "main", "_posts/new.md", "tok");

        Assert.True(exists.Success);
        Assert.True(exists.Data);
        Assert.True(missing.Success);
        Assert.False(missing.Data);
    }

    [Fact]
    public async Task CommitFilesAsync_ExecutesSixStepAtomicGitDataSequence()
    {
        int blobCounter = 0;
        var handler = new MockHttpHandler
        {
            Handler = (req, _) =>
            {
                var url = req.RequestUri!.ToString();
                if (req.Method == HttpMethod.Get && url.EndsWith("/git/ref/heads/main"))
                {
                    return JsonOk("""{"ref":"refs/heads/main","object":{"sha":"head-sha","type":"commit"}}""");
                }
                if (req.Method == HttpMethod.Get && url.EndsWith("/git/commits/head-sha"))
                {
                    return JsonOk("""{"sha":"head-sha","tree":{"sha":"base-tree-sha"}}""");
                }
                if (req.Method == HttpMethod.Post && url.EndsWith("/git/blobs"))
                {
                    var idx = Interlocked.Increment(ref blobCounter);
                    return JsonOk($$"""{"sha":"blob-sha-{{idx}}"}""");
                }
                if (req.Method == HttpMethod.Post && url.EndsWith("/git/trees"))
                {
                    return JsonOk("""{"sha":"new-tree-sha"}""");
                }
                if (req.Method == HttpMethod.Post && url.EndsWith("/git/commits"))
                {
                    return JsonOk("""{"sha":"new-commit-sha","html_url":"https://github.com/alice/blog/commit/new-commit-sha"}""");
                }
                if (req.Method == HttpMethod.Patch && url.EndsWith("/git/refs/heads/main"))
                {
                    return JsonOk("""{"ref":"refs/heads/main","object":{"sha":"new-commit-sha"}}""");
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
        };

        var client = new GitHubApiClient(new HttpClient(handler));
        var creds = new GitHubBlogCredentials("alice", "blog", "main", "ghp_tok");
        var files = new[]
        {
            GitHubCommitFile.FromBinary("assets/images/posts/hero.png", new byte[] { 1, 2, 3 }),
            GitHubCommitFile.FromText("_posts/2026-11-19-hello.md", "---\ntitle: Hello\n---\n\nBody")
        };

        var result = await client.CommitFilesAsync(creds, files, "Publish post: Hello");

        Assert.True(result.Success);
        Assert.Equal("new-commit-sha", result.Data!.Sha);
        Assert.Equal(7, handler.Requests.Count);
        Assert.Contains("\"encoding\":\"base64\"", handler.Requests[2].Body);
        Assert.Contains("\"encoding\":\"utf-8\"", handler.Requests[3].Body);
        Assert.Contains("\"base_tree\":\"base-tree-sha\"", handler.Requests[4].Body);
        Assert.Contains("\"tree\":\"new-tree-sha\"", handler.Requests[5].Body);
        Assert.Contains("\"sha\":\"new-commit-sha\"", handler.Requests[6].Body);
    }

    private static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
