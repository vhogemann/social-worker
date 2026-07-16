using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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

    private static string SliceUtf8ByBytes(string text, int byteStart, int byteEnd)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Encoding.UTF8.GetString(bytes, byteStart, byteEnd - byteStart);
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
    public async Task PublishAsync_ReturnsError_WhenReplyMetadataIsIncomplete()
    {
        using var db = CreateFreshDb(Options);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "reply-test-user",
            Email = "reply-test-user@example.com",
            PasswordHash = "hash"
        };
        db.Users.Add(user);
        var draftId = Guid.NewGuid();
        db.Drafts.Add(new Draft
        {
            Id = draftId,
            Title = "Reply Draft",
            UserId = user.Id,
            Content = "Hello"
        });

        db.DraftBlueskyMetadata.Add(new DraftBlueskyMetadata
        {
            DraftId = draftId,
            ReplyRootUri = "at://did:plc:root/app.bsky.feed.post/1",
            ReplyRootCid = "root-cid"
        });
        await db.SaveChangesAsync();

        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
            {
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            }
        });

        var result = await publisher.PublishAsync(new PlatformThread
        {
            DraftId = draftId,
            Content = "Hello"
        }, MakeAccount());

        Assert.False(result.Success);
        Assert.Contains("incomplete", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_UsesReplyMetadata_ForFirstPublishedSegment()
    {
        using var db = CreateFreshDb(Options);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "reply-success-user",
            Email = "reply-success-user@example.com",
            PasswordHash = "hash"
        };
        db.Users.Add(user);
        var draftId = Guid.NewGuid();
        db.Drafts.Add(new Draft
        {
            Id = draftId,
            Title = "Reply Success Draft",
            UserId = user.Id,
            Content = "First segment\n---\nSecond segment"
        });

        db.DraftBlueskyMetadata.Add(new DraftBlueskyMetadata
        {
            DraftId = draftId,
            ReplyRootUri = "at://did:plc:root/app.bsky.feed.post/100",
            ReplyRootCid = "root-cid",
            ReplyParentUri = "at://did:plc:parent/app.bsky.feed.post/101",
            ReplyParentCid = "parent-cid"
        });
        await db.SaveChangesAsync();

        var requestBodies = new List<string>();
        var (publisher, _) = CreatePublisher(db, ValidKey, (req, resp) =>
        {
            if (IsPath(req, "createSession"))
            {
                resp.Content = new StringContent(JsonSerializer.Serialize(new { accessJwt = "test-jwt", did = "did:plc:test" }));
            }
            else if (IsPath(req, "createRecord"))
            {
                requestBodies.Add(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
                var sequence = requestBodies.Count;
                resp.Content = new StringContent(JsonSerializer.Serialize(new { uri = $"at://did:plc:test/app.bsky.feed.post/{sequence}", cid = $"cid-{sequence}" }));
            }
        });

        var result = await publisher.PublishAsync(new PlatformThread
        {
            DraftId = draftId,
            Content = "First segment\n---\nSecond segment"
        }, MakeAccount());

        Assert.True(result.Success);
        Assert.Equal(2, requestBodies.Count);

        using var firstDoc = JsonDocument.Parse(requestBodies[0]);
        var firstReply = firstDoc.RootElement.GetProperty("record").GetProperty("reply");
        Assert.Equal("at://did:plc:root/app.bsky.feed.post/100", firstReply.GetProperty("root").GetProperty("uri").GetString());
        Assert.Equal("root-cid", firstReply.GetProperty("root").GetProperty("cid").GetString());
        Assert.Equal("at://did:plc:parent/app.bsky.feed.post/101", firstReply.GetProperty("parent").GetProperty("uri").GetString());
        Assert.Equal("parent-cid", firstReply.GetProperty("parent").GetProperty("cid").GetString());
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

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForMarkdownLinks()
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

        var content = "Read more at [The Atlantic](https://www.theatlantic.com/magazine/archive/2022/05/social-media-democracy-trust-babel/629369/)";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var text = record.GetProperty("text").GetString() ?? "";
        
        // The text should be plain without markdown
        Assert.Equal("Read more at The Atlantic", text);
        
        // Should have a facet for the link
        var facets = record.GetProperty("facets");
        Assert.NotEmpty(facets.EnumerateArray());
        
        var linkFacets = facets.EnumerateArray()
            .Where(f => f.GetProperty("features").EnumerateArray()
                .Any(feat => feat.GetProperty("$type").GetString() == "app.bsky.richtext.facet#link"))
            .ToList();
        
        Assert.Single(linkFacets);
        var linkFacet = linkFacets.First();
        var linkUrl = linkFacet.GetProperty("features")[0].GetProperty("uri").GetString();
        Assert.Equal("https://www.theatlantic.com/magazine/archive/2022/05/social-media-democracy-trust-babel/629369/", linkUrl);
    }

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForMarkdownLinksWithHashtags()
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

        var content = "Check out [this article](https://example.com/article) for more info. #Tech #AI";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var facets = record.GetProperty("facets");
        
        var facetTypes = facets.EnumerateArray()
            .SelectMany(f => f.GetProperty("features").EnumerateArray())
            .Select(f => f.GetProperty("$type").GetString())
            .ToList();
        
        // Should have both link and tag facets
        Assert.Contains("app.bsky.richtext.facet#link", facetTypes);
        Assert.Contains("app.bsky.richtext.facet#tag", facetTypes);
        
        // Should have 3 facets total (1 link + 2 tags)
        Assert.Equal(3, facets.GetArrayLength());
    }

    [Fact]
    public async Task PublishAsync_MarkdownLink_DoesNotIncludeClosingParen()
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

        var content = "See [docs](https://docs.example.com/guide) for details.";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var facets = record.GetProperty("facets");
        
        // Get the link facet
        var linkFacet = facets.EnumerateArray().First(f => 
            f.GetProperty("features")[0].GetProperty("$type").GetString() == "app.bsky.richtext.facet#link");
        
        var uri = linkFacet.GetProperty("features")[0].GetProperty("uri").GetString();
        
        // URL should NOT end with a paren
        Assert.False(uri?.EndsWith(")"), $"Link URL should not include closing paren: {uri}");
        Assert.Equal("https://docs.example.com/guide", uri);
    }

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForMultipleMarkdownLinks()
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

        var content = "Read [One](https://example.com/one) then [Two](https://example.com/two).";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var text = record.GetProperty("text").GetString() ?? "";
        Assert.Equal("Read One then Two.", text);

        var linkUris = record.GetProperty("facets").EnumerateArray()
            .SelectMany(f => f.GetProperty("features").EnumerateArray())
            .Where(f => f.GetProperty("$type").GetString() == "app.bsky.richtext.facet#link")
            .Select(f => f.GetProperty("uri").GetString())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        Assert.Equal(2, linkUris.Count);
        Assert.Contains("https://example.com/one", linkUris);
        Assert.Contains("https://example.com/two", linkUris);
    }

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForMarkdownLinksWithUtf8Prefix_ByteOffsetsMatchLinkText()
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

        var content = "Café 😊 [ümlaut](https://example.com/u)";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var text = record.GetProperty("text").GetString() ?? "";
        Assert.Equal("Café 😊 ümlaut", text);

        var linkFacet = record.GetProperty("facets").EnumerateArray().First(f =>
            f.GetProperty("features").EnumerateArray()
                .Any(feat => feat.GetProperty("$type").GetString() == "app.bsky.richtext.facet#link"));

        var byteStart = linkFacet.GetProperty("index").GetProperty("byteStart").GetInt32();
        var byteEnd = linkFacet.GetProperty("index").GetProperty("byteEnd").GetInt32();
        var slicedText = SliceUtf8ByBytes(text, byteStart, byteEnd);

        Assert.Equal("ümlaut", slicedText);
        Assert.Equal("https://example.com/u", linkFacet.GetProperty("features")[0].GetProperty("uri").GetString());
    }

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForMarkdownLinksAdjacentToPunctuation()
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

        var content = "See [docs](https://docs.example.com/guide), please.";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var text = record.GetProperty("text").GetString() ?? "";
        Assert.Equal("See docs, please.", text);

        var linkFacet = record.GetProperty("facets").EnumerateArray().First(f =>
            f.GetProperty("features").EnumerateArray()
                .Any(feat => feat.GetProperty("$type").GetString() == "app.bsky.richtext.facet#link"));

        var byteStart = linkFacet.GetProperty("index").GetProperty("byteStart").GetInt32();
        var byteEnd = linkFacet.GetProperty("index").GetProperty("byteEnd").GetInt32();
        var slicedText = SliceUtf8ByBytes(text, byteStart, byteEnd);

        Assert.Equal("docs", slicedText);
        Assert.Equal("https://docs.example.com/guide", linkFacet.GetProperty("features")[0].GetProperty("uri").GetString());
    }

    [Fact]
    public async Task PublishAsync_BuildsFacets_ForMarkdownLinksWithBareUrlAndHashtag()
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

        var content = "Canary: [Alpha](https://example.com/a) [Beta](https://example.com/b) https://example.com/c #LinkTest";
        var result = await publisher.PublishAsync(new PlatformThread { Content = content }, MakeAccount());

        Assert.True(result.Success);
        Assert.NotNull(capturedRecordBody);

        using var doc = JsonDocument.Parse(capturedRecordBody!);
        var record = doc.RootElement.GetProperty("record");
        var text = record.GetProperty("text").GetString() ?? "";
        Assert.Equal("Canary: Alpha Beta https://example.com/c #LinkTest", text);

        var facets = record.GetProperty("facets").EnumerateArray().ToList();

        var linkFacets = facets.Where(f =>
            f.GetProperty("features").EnumerateArray()
                .Any(feat => feat.GetProperty("$type").GetString() == "app.bsky.richtext.facet#link")).ToList();
        Assert.Equal(3, linkFacets.Count);

        var linkUriBySlice = new Dictionary<string, string?>();
        foreach (var linkFacet in linkFacets)
        {
            var byteStart = linkFacet.GetProperty("index").GetProperty("byteStart").GetInt32();
            var byteEnd = linkFacet.GetProperty("index").GetProperty("byteEnd").GetInt32();
            var slice = SliceUtf8ByBytes(text, byteStart, byteEnd);
            var uri = linkFacet.GetProperty("features")[0].GetProperty("uri").GetString();
            linkUriBySlice[slice] = uri;
        }

        Assert.Equal("https://example.com/a", linkUriBySlice["Alpha"]);
        Assert.Equal("https://example.com/b", linkUriBySlice["Beta"]);
        Assert.Equal("https://example.com/c", linkUriBySlice["https://example.com/c"]);

        var tagFacet = facets.First(f =>
            f.GetProperty("features").EnumerateArray()
                .Any(feat => feat.GetProperty("$type").GetString() == "app.bsky.richtext.facet#tag"));
        var tagByteStart = tagFacet.GetProperty("index").GetProperty("byteStart").GetInt32();
        var tagByteEnd = tagFacet.GetProperty("index").GetProperty("byteEnd").GetInt32();
        Assert.Equal("#LinkTest", SliceUtf8ByBytes(text, tagByteStart, tagByteEnd));
    }
}