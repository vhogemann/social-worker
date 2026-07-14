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

    [Fact]
    public async Task PublishAsync_YouTubeMarkdown_BuildsExternalEmbed()
    {
        using var db = CreateFreshDb(Options);
        string? capturedRecordBody = null;

        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
            {
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            }
            else if (req.RequestUri?.Host == "www.youtube.com" && req.RequestUri.PathAndQuery.StartsWith("/oembed", StringComparison.Ordinal))
            {
                resp.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    title = "AI Bubble Deep Dive",
                    thumbnail_url = "https://i.ytimg.com/vi/syJ7kjXfJ-U/hqdefault.jpg"
                }));
            }
            else if (req.RequestUri?.Host == "i.ytimg.com")
            {
                resp.Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            }
            else if (IsPath(req, "uploadBlob"))
            {
                resp.Content = new StringContent(JsonSerializer.Serialize(new { blob = new { cid = "thumb-cid", mimeType = "image/jpeg" } }));
            }
            else if (IsPath(req, "createRecord"))
            {
                capturedRecordBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                resp.Content = new StringContent(JsonSerializer.Serialize(new { uri = "at://did:plc:test/app.bsky.feed.post/abc", cid = "post-cid" }));
            }
        });

        var content = "![AI Bubble Deep Dive](https://www.youtube.com/watch?v=syJ7kjXfJ-U)\n\nThe AI investment bubble is real. Here are the key facts. #AIEconomics";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var embed = record.GetProperty("embed");
        Assert.Equal("app.bsky.embed.external", embed.GetProperty("$type").GetString());
        var external = embed.GetProperty("external");
        Assert.Equal("https://www.youtube.com/watch?v=syJ7kjXfJ-U", external.GetProperty("uri").GetString());
        Assert.Equal("AI Bubble Deep Dive", external.GetProperty("title").GetString());
        Assert.True(external.TryGetProperty("thumb", out _));

        var postText = record.GetProperty("text").GetString() ?? "";
        Assert.DoesNotContain("![", postText);
    }

    [Fact]
    public async Task PublishAsync_YouTubeMarkdown_SucceedsWithoutThumbnail_WhenOEmbedFails()
    {
        using var db = CreateFreshDb(Options);
        string? capturedRecordBody = null;

        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
            {
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            }
            else if (req.RequestUri?.Host == "www.youtube.com")
            {
                resp.StatusCode = System.Net.HttpStatusCode.NotFound;
            }
            else if (IsPath(req, "createRecord"))
            {
                capturedRecordBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                resp.Content = new StringContent(JsonSerializer.Serialize(new { uri = "at://did:plc:test/app.bsky.feed.post/abc", cid = "post-cid" }));
            }
        });

        var content = "![Watch this](https://www.youtube.com/watch?v=syJ7kjXfJ-U)\n\nSome text.";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var embed = record.GetProperty("embed");
        Assert.Equal("app.bsky.embed.external", embed.GetProperty("$type").GetString());
        var external = embed.GetProperty("external");
        Assert.Equal("https://www.youtube.com/watch?v=syJ7kjXfJ-U", external.GetProperty("uri").GetString());
        Assert.Equal("Watch this", external.GetProperty("title").GetString());
        Assert.False(external.TryGetProperty("thumb", out _));
    }

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForHashtagsAndUrls()
    {
        using var db = CreateFreshDb(Options);
        string? capturedRecordBody = null;

        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            else if (IsPath(req, "createRecord"))
            {
                capturedRecordBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                resp.Content = new StringContent(JsonSerializer.Serialize(new { uri = "at://did:plc:test/app.bsky.feed.post/abc", cid = "post-cid" }));
            }
        });

        var content = "AI is changing everything. Check https://example.com for more. #AI #Tech";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var facets = record.GetProperty("facets");
        Assert.True(facets.GetArrayLength() >= 3);

        var types = facets.EnumerateArray()
            .SelectMany(f => f.GetProperty("features").EnumerateArray())
            .Select(f => f.GetProperty("$type").GetString())
            .ToList();
        Assert.Contains("app.bsky.richtext.facet#tag", types);
        Assert.Contains("app.bsky.richtext.facet#link", types);
    }
}