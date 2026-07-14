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
    private static readonly byte[] TinyPngBytes =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
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
                Content = new ByteArrayContent(TinyPngBytes)
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
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