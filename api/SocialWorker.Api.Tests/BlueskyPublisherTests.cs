using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Infrastructure.Background;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class BlueskyPublisherTests : SqliteTestBase
{
    private static AppDbContext CreateFreshDb(DbContextOptions<AppDbContext> options)
    {
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static readonly string ValidKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private static (BlueskyPublisher Publisher, HttpClient Client) CreatePublisher(
        AppDbContext db, string? encryptionKey = null, Action<HttpRequestMessage, HttpResponseMessage>? intercept = null)
    {
        var configData = new Dictionary<string, string?>();
        if (encryptionKey != null)
            configData["Auth:DbEncryptionKey"] = encryptionKey;
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            intercept?.Invoke(req, resp);
            return resp;
        });
        var client = new HttpClient(handler);
        var storage = new FileStorageProvider();
        var publisher = new BlueskyPublisher(client, config, db, storage);
        return (publisher, client);
    }

    private static Account MakeAccount(string handle = "test.bsky.social")
    {
        return new Account
        {
            Handle = handle,
            CredentialsEncrypted = CryptoHelper.EncryptString("test-password", ValidKey)
        };
    }

    private static HttpResponseMessage JsonResponse(object body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body))
        };
    }

    private static bool IsPath(HttpRequestMessage req, string suffix)
    {
        return req.RequestUri?.AbsolutePath?.EndsWith(suffix, StringComparison.Ordinal) == true;
    }

    [Fact]
    public async Task PublishAsync_ReturnsError_WhenEncryptionKeyMissing()
    {
        using var db = CreateFreshDb(Options);
        var (publisher, _) = CreatePublisher(db);
        var result = await publisher.PublishAsync(new PlatformThread { Content = "Hello" }, new Account());
        Assert.False(result.Success);
        Assert.Contains("encryption key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_ReturnsError_WhenDecryptionFails()
    {
        using var db = CreateFreshDb(Options);
        var (publisher, _) = CreatePublisher(db, "invalid-key");
        var result = await publisher.PublishAsync(new PlatformThread { Content = "Hello" }, new Account { CredentialsEncrypted = "garbage" });
        Assert.False(result.Success);
        Assert.Contains("decrypt", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_ReturnsError_WhenAuthFails()
    {
        using var db = CreateFreshDb(Options);
        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
            {
                resp.StatusCode = HttpStatusCode.Unauthorized;
                resp.Content = new StringContent("Invalid credentials");
            }
        });
        var result = await publisher.PublishAsync(new PlatformThread { Content = "Hello" }, MakeAccount());
        Assert.False(result.Success);
        Assert.Contains("authenticate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_SuccessfullyPublishesSingleSegment()
    {
        using var db = CreateFreshDb(Options);
        int createRecordCalls = 0;

        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            else if (IsPath(req, "createRecord"))
            {
                createRecordCalls++;
                resp.Content = new StringContent(JsonSerializer.Serialize(new { uri = "at://did:plc:test/app.bsky.feed.post/123", cid = "test-cid" }));
            }
        });

        var result = await publisher.PublishAsync(new PlatformThread { Content = "Testing Bluesky publishing from social-worker!" }, MakeAccount());
        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal(0, result.Posts[0].SegmentIndex);
        Assert.Equal(1, createRecordCalls);
    }

    [Fact]
    public async Task PublishAsync_SuccessfullyPublishesMultiSegmentThread()
    {
        using var db = CreateFreshDb(Options);
        int createRecordCalls = 0;

        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            else if (IsPath(req, "createRecord"))
            {
                createRecordCalls++;
                resp.Content = new StringContent(JsonSerializer.Serialize(new { uri = $"at://did:plc:test/app.bsky.feed.post/{createRecordCalls}", cid = $"test-cid-{createRecordCalls}" }));
            }
        });

        var result = await publisher.PublishAsync(new PlatformThread
        {
            Content = "First segment.\n\n---\n\nSecond segment.\n\n---\n\nThird segment."
        }, MakeAccount());

        Assert.True(result.Success);
        Assert.Equal(3, result.Posts.Count);
        Assert.Equal(3, createRecordCalls);
    }
}