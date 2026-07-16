using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ChatSessionLoaderTests : SqliteTestBase
{
    private static async Task<(AppDbContext Db, ChatSessionLoader Loader, AppUser User, LlmProvider Provider)> SetupAsync(AppDbContext db)
    {
        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h", IsActive = true };
        db.Users.Add(user);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(), Name = "Test", ProviderType = "OpenAI",
            BaseUrl = "https://test.local/v1", ApiKey = "key", Model = "gpt-4o",
            IsDefault = true, IsActive = true
        };
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var probe = new ModelCapabilityProbe(null!, cache, NullLogger<ModelCapabilityProbe>.Instance);
        var titleGen = new DraftTitleGenerator(new DemoLlmAdapter(), NullLogger<DraftTitleGenerator>.Instance);
        var providerSvc = new LlmProviderService();

        var loader = new ChatSessionLoader(scopeFactory, probe, titleGen, providerSvc, null!);
        return (db, loader, user, provider);
    }

    [Fact]
    public async Task LoadAsync_Throws_For_InactiveUser()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        db.Users.Add(new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h", IsActive = false });
        db.LlmProviders.Add(new LlmProvider { Id = Guid.NewGuid(), Name = "T", ProviderType = "OAI", BaseUrl = "https://t", Model = "m", IsDefault = true, IsActive = true });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var loader = new ChatSessionLoader(scopeFactory, null!, null!, new LlmProviderService(), null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            loader.LoadAsync(Guid.NewGuid(), null, null, new(), CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenNoProvider()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        db.Users.Add(new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h", IsActive = true });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var loader = new ChatSessionLoader(scopeFactory, null!, null!, new LlmProviderService(), null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            loader.LoadAsync(Guid.NewGuid(), null, null, new(), CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_CreatesNewDraft_WhenNoDraftId()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, loader, user, _) = await SetupAsync(db);

        var ctx = await loader.LoadAsync(user.Id, null, null, new(), CancellationToken.None);

        Assert.NotNull(ctx.Draft);
        Assert.Equal("", ctx.EditorContent);
    }

    [Fact]
    public async Task LoadAsync_CreatesNewDraft_WithEditorContent()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, loader, user, _) = await SetupAsync(db);

        var ctx = await loader.LoadAsync(user.Id, null, "Hello world", new(), CancellationToken.None);

        Assert.Equal("Hello world", ctx.EditorContent);
    }

    [Fact]
    public async Task LoadAsync_LoadsExistingDraft()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, loader, user, _) = await SetupAsync(db);

        var draft = new Draft { Id = Guid.NewGuid(), Title = "Existing", Content = "Existing content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var ctx = await loader.LoadAsync(user.Id, draft.Id, null, new(), CancellationToken.None);

        Assert.Equal(draft.Id, ctx.Draft.Id);
        Assert.Equal("Existing content", ctx.EditorContent);
    }

    [Fact]
    public async Task LoadAsync_Throws_For_DeletedDraft()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, loader, user, _) = await SetupAsync(db);

        var draft = new Draft { Id = Guid.NewGuid(), Title = "Del", Content = "C", UserId = user.Id, Status = DraftStatus.Deleted };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            loader.LoadAsync(user.Id, draft.Id, null, new(), CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_UpdatesExistingContent_WhenEditorContentDiffers()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, loader, user, _) = await SetupAsync(db);

        var draft = new Draft { Id = Guid.NewGuid(), Title = "T", Content = "Old content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var ctx = await loader.LoadAsync(user.Id, draft.Id, "New content", new(), CancellationToken.None);

        Assert.Equal("New content", ctx.EditorContent);
        var reloaded = await db.Drafts.FindAsync(draft.Id);
        Assert.Equal("New content", reloaded!.Content);
    }

    [Fact]
    public async Task LoadAsync_LoadsDefaultBrandVoice()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, loader, user, _) = await SetupAsync(db);

        var bv = new BrandVoicePrompt { Id = Guid.NewGuid(), UserId = user.Id, Name = "Pro", Body = "Write professionally.", IsDefault = true };
        db.BrandVoicePrompts.Add(bv);
        await db.SaveChangesAsync();

        var ctx = await loader.LoadAsync(user.Id, null, null, new(), CancellationToken.None);

        Assert.Equal("Write professionally.", ctx.DefaultBrandVoiceBody);
    }
}