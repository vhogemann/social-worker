using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.LinkedIn;

public sealed class LinkedInPublishServiceTests : SqliteTestBase
{
    private static AppDbContext CreateFreshDb(DbContextOptions<AppDbContext> options)
    {
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static HttpResponseMessage JsonResponse(object body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _sender;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sender)
        {
            _sender = sender;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_sender(request));
        }
    }

    [Fact]
    public async Task PublishAsync_TextOnly_Succeeds()
    {
        using var db = CreateFreshDb(Options);
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("ugcPosts"))
            {
                return JsonResponse(new LinkedInUgcPostResponse { Id = "urn:li:share:12345" });
            }
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var client = new HttpClient(handler);
        var apiClient = new LinkedInApiClient(client);
        var storage = new FileStorageProvider();
        var service = new LinkedInPublishService(db, storage, apiClient);

        var result = await service.PublishAsync(
            content: "Hello LinkedIn!",
            authorUrn: "urn:li:person:abcdef",
            visibility: "PUBLIC",
            draftId: Guid.Empty,
            threadId: Guid.Empty,
            token: "my-token",
            ct: default
        );

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("urn:li:share:12345", result.Posts[0].RemoteId);
        Assert.Equal("https://www.linkedin.com/feed/update/urn:li:share:12345", result.Posts[0].Url);
    }

    [Fact]
    public async Task PublishAsync_ImageUploadAndPublish_Succeeds()
    {
        using var db = CreateFreshDb(Options);
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, targetPlatform: SocialPlatform.LinkedIn);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var mediaId = Guid.NewGuid();
        var asset = new MediaAsset
        {
            Id = mediaId,
            DraftId = draft.Id,
            FilePath = "test-images/pic.png",
            MimeType = "image/png",
            AltText = "Database Alt Text"
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var storage = new FileStorageProvider();
        var dummyBytes = new byte[] { 1, 2, 3, 4 };
        await storage.WriteFileAsync("test-images/pic.png", dummyBytes);

        string? capturedRegisterBody = null;
        string? capturedUploadAuthHeader = null;
        string? capturedUploadCustomHeader = null;
        string? capturedUploadContentType = null;
        string? capturedUgcBody = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("registerUpload"))
            {
                capturedRegisterBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var rawLinkedInResponse = """
                    {
                      "value": {
                        "asset": "urn:li:digitalmediaAsset:1111",
                        "uploadMechanism": {
                          "com.linkedin.digitalmedia.uploading.MediaUploadHttpRequest": {
                            "uploadUrl": "https://api.linkedin.com/upload-here",
                            "headers": {
                              "media-type-family": "STILLIMAGE"
                            }
                          }
                        }
                      }
                    }
                    """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(rawLinkedInResponse, Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("upload-here"))
            {
                capturedUploadAuthHeader = req.Headers.Authorization?.ToString();
                if (req.Headers.TryGetValues("media-type-family", out var values))
                {
                    capturedUploadCustomHeader = string.Join(",", values);
                }
                capturedUploadContentType = req.Content?.Headers.ContentType?.MediaType;
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            if (req.RequestUri!.AbsolutePath.Contains("ugcPosts"))
            {
                capturedUgcBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse(new LinkedInUgcPostResponse { Id = "urn:li:share:999" });
            }
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var client = new HttpClient(handler);
        var apiClient = new LinkedInApiClient(client);
        var service = new LinkedInPublishService(db, storage, apiClient);

        try
        {
            var result = await service.PublishAsync(
                content: $"Hello image! ![Cool image](media://{mediaId})",
                authorUrn: "urn:li:person:abcdef",
                visibility: "PUBLIC",
                draftId: draft.Id,
                threadId: Guid.Empty,
                token: "my-token",
                ct: default
            );

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Single(result.Posts);
            Assert.Equal("urn:li:share:999", result.Posts[0].RemoteId);

            Assert.NotNull(capturedRegisterBody);
            using var registerDoc = JsonDocument.Parse(capturedRegisterBody!);
            Assert.True(registerDoc.RootElement.TryGetProperty("registerUploadRequest", out var registerReq));
            Assert.Equal("urn:li:person:abcdef", registerReq.GetProperty("owner").GetString());
            Assert.Equal("urn:li:digitalmediaRecipe:feedshare-image", registerReq.GetProperty("recipes")[0].GetString());
            Assert.Equal("SYNCHRONOUS_UPLOAD", registerReq.GetProperty("supportedUploadMechanism")[0].GetString());
            var relationships = registerReq.GetProperty("serviceRelationships");
            Assert.Equal(1, relationships.GetArrayLength());
            Assert.Equal("OWNER", relationships[0].GetProperty("relationshipType").GetString());
            Assert.Equal("urn:li:userGeneratedContent", relationships[0].GetProperty("identifier").GetString());

            Assert.Equal("Bearer my-token", capturedUploadAuthHeader);
            Assert.Equal("STILLIMAGE", capturedUploadCustomHeader);
            Assert.Equal("image/png", capturedUploadContentType);

            Assert.NotNull(capturedUgcBody);
            using var ugcDoc = JsonDocument.Parse(capturedUgcBody!);
            var shareContent = ugcDoc.RootElement
                .GetProperty("specificContent")
                .GetProperty("com.linkedin.ugc.ShareContent");
            Assert.Equal("IMAGE", shareContent.GetProperty("shareMediaCategory").GetString());
            Assert.Equal("urn:li:digitalmediaAsset:1111", shareContent.GetProperty("media")[0].GetProperty("media").GetString());
        }
        finally
        {
            storage.DeleteFileAndEmptyFolder("test-images/pic.png");
        }
    }

    [Fact]
    public async Task PublishAsync_WithInvalidContent_FailsFast()
    {
        using var db = CreateFreshDb(Options);
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = new HttpClient(handler);
        var apiClient = new LinkedInApiClient(client);
        var storage = new FileStorageProvider();
        var service = new LinkedInPublishService(db, storage, apiClient);

        var result = await service.PublishAsync(
            content: "Segment 1\n---\nSegment 2",
            authorUrn: "urn:li:person:abcdef",
            visibility: "PUBLIC",
            draftId: Guid.Empty,
            threadId: Guid.Empty,
            token: "my-token",
            ct: default
        );

        Assert.False(result.Success);
        Assert.Contains("LinkedIn publishing does not support multi-post threads", result.ErrorMessage);
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublished_ReturnsExistingWithoutCallingApi()
    {
        using var db = CreateFreshDb(Options);
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, targetPlatform: SocialPlatform.LinkedIn);
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "LinkedIn",
            Content = "Hello LinkedIn!",
            Stage = PlatformThreadStage.Sent
        };
        db.PlatformThreads.Add(thread);

        var existingPost = new Post
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            SegmentIndex = 0,
            Platform = "LinkedIn",
            RemoteId = "urn:li:share:already-sent",
            PublishedText = "Hello LinkedIn!",
            Url = "https://www.linkedin.com/feed/update/urn:li:share:already-sent",
            PostedAt = DateTime.UtcNow
        };
        db.Posts.Add(existingPost);
        await db.SaveChangesAsync();

        bool apiWasCalled = false;
        var handler = new MockHttpMessageHandler(req =>
        {
            apiWasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var client = new HttpClient(handler);
        var apiClient = new LinkedInApiClient(client);
        var storage = new FileStorageProvider();
        var service = new LinkedInPublishService(db, storage, apiClient);

        var result = await service.PublishAsync(
            content: "Hello LinkedIn!",
            authorUrn: "urn:li:person:abcdef",
            visibility: "PUBLIC",
            draftId: draft.Id,
            threadId: thread.Id,
            token: "my-token",
            ct: default
        );

        Assert.True(result.Success);
        Assert.False(apiWasCalled);
        Assert.Single(result.Posts);
        Assert.Equal("urn:li:share:already-sent", result.Posts[0].RemoteId);
    }
}
