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
using SocialWorker.Api.Infrastructure.Background;
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
        serviceCollection.AddSingleton<BackgroundJobQueue>();
        serviceCollection.AddSingleton<SourcesService>();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var tool = new AddSourceTool(scopeFactory);
        var args = new AddSourceArgs("Url", "https://example.com/testpage", null, null, null);

        var response = await tool.ExecuteAsync(args, draft.Id, userId, CancellationToken.None);

        Assert.Contains("Successfully added source", response);

        var inserted = await db.Sources.FirstOrDefaultAsync(s => s.DraftSources.Any(ds => ds.DraftId == draft.Id));
        Assert.NotNull(inserted);
        Assert.Equal(SourceKind.Url, inserted.Kind);
        Assert.Equal("https://example.com/testpage", inserted.Reference);
        Assert.Equal("Page Title", inserted.Title);
        Assert.Contains("Extracted Body Content", inserted.Content ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Error_For_Relative_Url_And_Does_Not_Insert_Source()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "My Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var client = new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var scraper = new WebScraperService(client);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(scraper);
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<SourcesService>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var tool = new AddSourceTool(scopeFactory);
        var result = await tool.ExecuteAsync(new AddSourceArgs("Url", "/relative/path", null, null, null), draft.Id, userId, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Empty(await db.Sources.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Error_When_Scrape_Fails_And_Does_Not_Insert_Source()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "My Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var client = new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var scraper = new WebScraperService(client);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(scraper);
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<SourcesService>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var tool = new AddSourceTool(scopeFactory);
        var result = await tool.ExecuteAsync(new AddSourceArgs("Url", "https://example.com/missing", null, null, null), draft.Id, userId, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Empty(await db.Sources.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_Links_Existing_Source_By_SourceId()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft1 = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft 1", Status = DraftStatus.Editing };
        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft 2", Status = DraftStatus.Editing };
        db.Drafts.AddRange(draft1, draft2);
        var source = new Source { Kind = SourceKind.Url, Reference = "https://example.com", Title = "Existing Source", Content = "content" };
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { Draft = draft1, Source = source });
        await db.SaveChangesAsync();

        var client = new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var scraper = new WebScraperService(client);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(scraper);
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<SourcesService>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var tool = new AddSourceTool(scopeFactory);
        var result = await tool.ExecuteAsync(new AddSourceArgs(null, null, null, null, source.Id), draft2.Id, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Linked existing source", result.Message);

        var link = await db.DraftSources.FirstOrDefaultAsync(ds => ds.DraftId == draft2.Id && ds.SourceId == source.Id);
        Assert.NotNull(link);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Error_When_Neither_SourceId_Nor_Kind_Provided()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var client = new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var scraper = new WebScraperService(client);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(scraper);
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<SourcesService>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var tool = new AddSourceTool(scopeFactory);
        var result = await tool.ExecuteAsync(new AddSourceArgs(null, null, null, null, null), draft.Id, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("source_id or kind+reference", result.Error);
    }
}
