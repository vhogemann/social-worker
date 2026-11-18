using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Instagram;
using SocialWorker.Api.Features.Sources.Instagram;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Instagram;

public sealed class InstagramPublisherTests : SqliteTestBase
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> RequestBodies { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
            RequestBodies.Add(body);
            return Handler(request, body);
        }
    }

    private (InstagramPublisher Publisher, MockHttpMessageHandler HttpHandler, AppDbContext Db) CreatePublisher()
    {
        var db = CreateDbContext();
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = EncryptionKey,
                ["App:BaseUrl"] = "https://app.test.com"
            })
            .Build();

        var options = Microsoft.Extensions.Options.Options.Create(new InstagramOptions());
        var apiClient = new InstagramApiClient(httpClient, options, NullLogger<InstagramApiClient>.Instance);
        var publisher = new InstagramPublisher(config, db, apiClient, NullLogger<InstagramPublisher>.Instance);
        return (publisher, handler, db);
    }

    private static Account CreateInstagramAccount(string externalId = "ig_user_1")
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Platform = "Instagram",
            Handle = "@testcreator",
            ExternalId = externalId,
            CredentialsEncrypted = CryptoHelper.EncryptString("ig_access_token", EncryptionKey)
        };
    }

    [Fact]
    public async Task PublishAsync_SingleImage_CreatesPollsAndPublishesWithPermalink()
    {
        var (publisher, handler, db) = CreatePublisher();
        var account = CreateInstagramAccount();
        var mediaId = Guid.NewGuid();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = mediaId,
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            FilePath = "/data/media/photo.jpg",
            Width = 1080,
            Height = 1080
        });
        await db.SaveChangesAsync();

        handler.Handler = (req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.EndsWith("/media"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"c_single\"}") };
            if (url.Contains("/c_single?fields=id,status_code"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"c_single\",\"status_code\":\"FINISHED\"}") };
            if (url.EndsWith("/media_publish"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"ig_post_1\"}") };
            if (url.Contains("/ig_post_1?fields=id,permalink"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"ig_post_1\",\"permalink\":\"https://www.instagram.com/p/ABC123/\"}") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            Platform = "Instagram",
            Content = $"Golden hour on the coast!\n![Sunset](media://{mediaId})"
        };

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("ig_post_1", result.Posts[0].RemoteId);
        Assert.Equal("https://www.instagram.com/p/ABC123/", result.Posts[0].Url);
        Assert.Equal("Golden hour on the coast!", result.Posts[0].PublishedText);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task PublishAsync_CarouselTwoSegments_PublishesCarouselWithSegment0Caption()
    {
        var (publisher, handler, db) = CreatePublisher();
        var account = CreateInstagramAccount();
        var media1 = Guid.NewGuid();
        var media2 = Guid.NewGuid();
        db.MediaAssets.AddRange(
            new MediaAsset { Id = media1, FileName = "1.jpg", MimeType = "image/jpeg", FilePath = "/1.jpg", Width = 1080, Height = 1350 },
            new MediaAsset { Id = media2, FileName = "2.jpg", MimeType = "image/jpeg", FilePath = "/2.jpg", Width = 1080, Height = 1350 }
        );
        await db.SaveChangesAsync();

        int childCounter = 0;
        handler.Handler = (req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.EndsWith("/media"))
            {
                if (body.Contains("media_type=CAROUSEL"))
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"c_parent\"}") };
                childCounter++;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"id\":\"c_child_{childCounter}\"}}") };
            }
            if (url.Contains("fields=id,status_code"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"c\",\"status_code\":\"FINISHED\"}") };
            if (url.EndsWith("/media_publish"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"ig_carousel_1\"}") };
            if (url.Contains("/ig_carousel_1?fields=id,permalink"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"ig_carousel_1\",\"shortcode\":\"Car123\"}") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            Platform = "Instagram",
            Content = $"Carousel caption\n![First](media://{media1})\n---\n![Second](media://{media2})"
        };

        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("ig_carousel_1", result.Posts[0].RemoteId);
        Assert.Equal("https://www.instagram.com/p/Car123/", result.Posts[0].Url);
        Assert.Equal("Carousel caption", result.Posts[0].PublishedText);
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublished_ReturnsExistingPostsIdempotentlyWithoutApiCalls()
    {
        var (publisher, handler, db) = CreatePublisher();
        var user = CreateSeedUser(db);
        var account = CreateInstagramAccount();
        account.UserId = user.Id;
        var draftId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        db.Drafts.Add(new Draft { Id = draftId, UserId = user.Id, Title = "Test", Content = "Hello" });
        db.PlatformThreads.Add(new PlatformThread { Id = threadId, DraftId = draftId, Platform = "Instagram", Content = "Hello" });
        db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draftId,
            PlatformThreadId = threadId,
            Platform = "Instagram",
            SegmentIndex = 0,
            RemoteId = "ig_existing",
            PublishedText = "Hello",
            Url = "https://www.instagram.com/p/Existing/"
        });
        await db.SaveChangesAsync();

        var thread = new PlatformThread { Id = threadId, DraftId = draftId, Platform = "Instagram", Content = "Hello" };
        var result = await publisher.PublishAsync(thread, account);

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("ig_existing", result.Posts[0].RemoteId);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PublishAsync_MissingExternalId_FailsWithClearMessage()
    {
        var (publisher, _, _) = CreatePublisher();
        var account = CreateInstagramAccount(externalId: "");
        var thread = new PlatformThread { Id = Guid.NewGuid(), Platform = "Instagram", Content = "Hi" };

        var result = await publisher.PublishAsync(thread, account);

        Assert.False(result.Success);
        Assert.Contains("ExternalId", result.ErrorMessage);
    }

    [Fact]
    public async Task PublishAsync_NoImages_Fails()
    {
        var (publisher, _, _) = CreatePublisher();
        var account = CreateInstagramAccount();
        var thread = new PlatformThread { Id = Guid.NewGuid(), Platform = "Instagram", Content = "Text only post" };

        var result = await publisher.PublishAsync(thread, account);

        Assert.False(result.Success);
        Assert.Contains("at least one image", result.ErrorMessage);
    }

    [Fact]
    public async Task PublishAsync_TextInSecondSegment_Fails()
    {
        var (publisher, _, _) = CreatePublisher();
        var account = CreateInstagramAccount();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            Platform = "Instagram",
            Content = $"Cap\n![A](media://{m1})\n---\nExtra text\n![B](media://{m2})"
        };

        var result = await publisher.PublishAsync(thread, account);

        Assert.False(result.Success);
        Assert.Contains("segment 2 must not contain caption text", result.ErrorMessage);
    }
}
