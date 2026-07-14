using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Media;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ViewImageToolTests : SqliteTestBase
{
    private static readonly byte[] TinyJpegBytes =
    {
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
        0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
        0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
        0x00, 0x03, 0x02, 0x02, 0x03, 0x02, 0x02, 0x03,
        0x03, 0x03, 0x03, 0x04, 0x03, 0x03, 0x04, 0x05,
        0x08, 0x05, 0x05, 0x04, 0x04, 0x05, 0x0A, 0x07,
        0x07, 0x06, 0x08, 0x0C, 0x0A, 0x0C, 0x0C, 0x0B,
        0x0A, 0x0B, 0x0B, 0x0D, 0x0E, 0x12, 0x10, 0x0D,
        0x0E, 0x11, 0x0E, 0x0B, 0x0B, 0x10, 0x16, 0x10,
        0x11, 0x13, 0x14, 0x15, 0x15, 0x15, 0x0C, 0x0F,
        0x17, 0x18, 0x16, 0x14, 0x18, 0x12, 0x14, 0x15,
        0x14, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
        0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4,
        0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x09, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
        0x00, 0x00, 0x3F, 0x00, 0xD2, 0xCF, 0x20, 0xFF,
        0xD9
    };

    [Fact]
    public async Task ExecuteAsync_Throws_ForInvalidId()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var tool = new ViewImageTool(sp.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tool.ExecuteAsync(new ViewImageArgs("bad-id"), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_ImportsDirectUrl_WhenHttpUrlIsProvided()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Image Draft",
            Status = DraftStatus.Editing
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var httpHandler = new LocalMockHttpMessageHandler(_ =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(TinyJpegBytes)
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return res;
        });

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(new ImageResizer());
        services.AddSingleton(new FileStorageProvider());
        services.AddSingleton<MediaService>();
        services.AddSingleton<IHttpClientFactory>(new LocalMockHttpClientFactory(new HttpClient(httpHandler)));

        using var provider = services.BuildServiceProvider();
        var tool = new ViewImageTool(provider.GetRequiredService<IServiceScopeFactory>());

        var result = await tool.ExecuteAsync(
            new ViewImageArgs("https://example.com/heatwave.png"),
            draft.Id,
            user.Id,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("text", result[0].Type);
        Assert.Contains("Image:", result[0].Text ?? string.Empty);
        Assert.Equal("image_url", result[1].Type);
        Assert.StartsWith("data:image/jpeg;base64,", result[1].ImageUrl?.Url ?? string.Empty, StringComparison.Ordinal);

        var storedAsset = await db.MediaAssets.FirstOrDefaultAsync(m => m.DraftId == draft.Id);
        Assert.NotNull(storedAsset);
    }

    private sealed class LocalMockHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public LocalMockHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private sealed class LocalMockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public LocalMockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}