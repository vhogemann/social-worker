using System;
using System.Linq;
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

public sealed class SearchSourcesToolTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SearchSourcesToolTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private (IServiceScopeFactory scopeFactory, AppDbContext db) BuildServices()
    {
        var db = new AppDbContext(_options);
        var client = new System.Net.Http.HttpClient(new MockHttpMessageHandler(_ =>
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var scraper = new WebScraperService(client);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(scraper);
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton(TestServiceFactory.CreateSourcesService(db, scraper: scraper));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IServiceScopeFactory>(), db);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Matching_Sources()
    {
        var (scopeFactory, db) = BuildServices();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);

        var source = new Source { Kind = SourceKind.Url, Reference = "https://example.com", Title = "Climate Policy Report", Content = "Report on climate change policy" };
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { Draft = draft, Source = source });
        await db.SaveChangesAsync();

        var tool = new SearchSourcesTool(scopeFactory);
        var result = await tool.ExecuteAsync(new SearchSourcesArgs("climate", null), null, userId, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Climate Policy Report", result.Items[0].Title);
    }

    [Fact]
    public async Task ExecuteAsync_Excludes_Sources_Linked_To_Active_Draft()
    {
        var (scopeFactory, db) = BuildServices();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft1 = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft 1", Status = DraftStatus.Editing };
        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft 2", Status = DraftStatus.Editing };
        db.Drafts.AddRange(draft1, draft2);

        var linkedSource = new Source { Kind = SourceKind.Url, Reference = "https://linked.com", Title = "Linked Source", Content = "Already linked content" };
        var unlinkedSource = new Source { Kind = SourceKind.Url, Reference = "https://unlinked.com", Title = "Unlinked Source", Content = "Available content" };
        db.Sources.AddRange(linkedSource, unlinkedSource);
        db.DraftSources.Add(new DraftSource { Draft = draft1, Source = linkedSource });
        db.DraftSources.Add(new DraftSource { Draft = draft2, Source = unlinkedSource });
        await db.SaveChangesAsync();

        var tool = new SearchSourcesTool(scopeFactory);
        var result = await tool.ExecuteAsync(new SearchSourcesArgs("content", null), draft1.Id, userId, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Unlinked Source", result.Items[0].Title);
    }

    [Fact]
    public async Task ExecuteAsync_Empty_Query_Returns_Empty()
    {
        var (scopeFactory, db) = BuildServices();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var tool = new SearchSourcesTool(scopeFactory);
        var result = await tool.ExecuteAsync(new SearchSourcesArgs("", null), null, userId, CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ExecuteAsync_No_Match_Returns_Empty()
    {
        var (scopeFactory, db) = BuildServices();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        var source = new Source { Kind = SourceKind.Url, Reference = "https://example.com", Title = "Ocean Ecology", Content = "Marine biology" };
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { Draft = draft, Source = source });
        await db.SaveChangesAsync();

        var tool = new SearchSourcesTool(scopeFactory);
        var result = await tool.ExecuteAsync(new SearchSourcesArgs("cryptocurrency", null), null, userId, CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ExecuteAsync_Respects_Limit()
    {
        var (scopeFactory, db) = BuildServices();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);

        for (int i = 0; i < 10; i++)
        {
            var s = new Source { Kind = SourceKind.Url, Reference = $"https://example.com/{i}", Title = $"Topic Source {i}", Content = "Topic content" };
            db.Sources.Add(s);
            db.DraftSources.Add(new DraftSource { Draft = draft, Source = s });
        }
        await db.SaveChangesAsync();

        var tool = new SearchSourcesTool(scopeFactory);
        var result = await tool.ExecuteAsync(new SearchSourcesArgs("topic", 3), null, userId, CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
    }
}
