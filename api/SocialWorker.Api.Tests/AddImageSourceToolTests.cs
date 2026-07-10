using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Media;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class AddImageSourceToolTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    private static readonly byte[] TinyPngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    public AddImageSourceToolTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_Downloads_And_Adds_Image_Successfully()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "My Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var httpHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(TinyPngBytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return res;
        });
        var client = new HttpClient(httpHandler);
        var mockHttpClientFactory = new MockHttpClientFactory(client);

        var storage = new FileStorageProvider();
        var resizer = new ImageResizer();
        var mediaService = new MediaService(db, resizer, storage);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(db);
        serviceCollection.AddSingleton(mediaService);
        serviceCollection.AddSingleton<IHttpClientFactory>(mockHttpClientFactory);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var tool = new AddImageSourceTool(scopeFactory);
        var args = new AddImageSourceArgs("https://example.com/pineapple.png", "Pineapple Alt Text");

        var response = await tool.ExecuteAsync(args, draft.Id, userId, CancellationToken.None);

        Assert.Contains("Successfully imported image", response);
        Assert.Contains("media://", response);

        var asset = await db.MediaAssets.FirstOrDefaultAsync(m => m.DraftId == draft.Id);
        Assert.NotNull(asset);
        Assert.Equal("Pineapple Alt Text", asset.AltText);
        Assert.Equal("image/png", asset.MimeType);
    }
}

public class MockHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public MockHttpClientFactory(HttpClient client) => _client = client;
    public HttpClient CreateClient(string name) => _client;
}
