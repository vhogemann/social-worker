using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Background;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ReplaceEditorContentToolTests : SqliteTestBase
{
    private static (AppDbContext Db, ReplaceEditorContentTool Tool) Create(AppDbContext db)
    {
        var services = new ServiceCollection();

        var storage = new FileStorageProvider();
        var queue = new BackgroundJobQueue();
        var scopeFact = new MockScopeFactory(db);
        var sourcesService = TestServiceFactory.CreateSourcesService(db, scopeFactory: scopeFact, queue: queue);
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService, scopeFact, queue);

        services.AddSingleton(db);
        services.AddSingleton<DraftsService>(draftsService);
        var sp = services.BuildServiceProvider();

        var toolScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var tool = new ReplaceEditorContentTool(toolScopeFactory);
        return (db, tool);
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesContent()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, tool) = Create(db);

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "T", Content = "Old content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var result = await tool.ExecuteAsync(new ReplaceEditorContentArgs("New content"), draft.Id, user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New content", result.Content);

        var reloaded = await db.Drafts.FindAsync(draft.Id);
        Assert.Equal("New content", reloaded!.Content);
    }
}