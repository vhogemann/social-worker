using System;
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
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class AddSourceToolTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AddSourceToolTests()
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
    public async Task ExecuteAsync_Successfully_Inserts_Url_Source()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "My Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var htmlResponse = "<html><head><title>Page Title</title></head><body>Extracted Body Content</body></html>";
        var httpHandler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlResponse)
        });
        var client = new HttpClient(httpHandler);
        var scraper = new WebScraperService(client);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(db);
        serviceCollection.AddSingleton(scraper);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var tool = new AddSourceTool(scopeFactory);
        var args = new AddSourceArgs("Url", "https://example.com/testpage", null, null);

        var response = await tool.ExecuteAsync(args, draft.Id, userId, CancellationToken.None);

        Assert.Contains("Successfully added source", response);

        var inserted = await db.Sources.FirstOrDefaultAsync(s => s.DraftId == draft.Id);
        Assert.NotNull(inserted);
        Assert.Equal(SourceKind.Url, inserted.Kind);
        Assert.Equal("https://example.com/testpage", inserted.Reference);
        Assert.Equal("Page Title", inserted.Title);
        Assert.Contains("Extracted Body Content", inserted.Content ?? "");
    }
}
