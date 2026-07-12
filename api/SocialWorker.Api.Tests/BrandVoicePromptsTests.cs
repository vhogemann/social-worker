using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class BrandVoicePromptsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public BrandVoicePromptsTests()
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
    public void SystemPromptBuilder_Appends_BrandVoice_If_Provided()
    {
        var builder = new SystemPromptBuilder();
        var prompt = builder.Build(
            customSystemPrompt: "Base prompt.",
            editorContent: "Content.",
            mediaAssets: new List<MediaAsset>(),
            supportsVision: false,
            brandVoiceBody: "Write like a pirate."
        );

        Assert.Contains("--- WRITING STYLE / BRAND VOICE ---", prompt);
        Assert.Contains("Write like a pirate.", prompt);
    }

    [Fact]
    public async Task ChatSessionLoader_Loads_Default_BrandVoice_Successfully()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        
        var user = new AppUser { Id = userId, Username = "test", Email = "test@e.com", PasswordHash = "hash" };
        db.Users.Add(user);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Default Provider",
            ProviderType = "Default",
            BaseUrl = "https://example.com/v1",
            ApiKey = "key",
            Model = "gpt-4o",
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var brandVoice = new BrandVoicePrompt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Pirate Style",
            Body = "Arrr, write like a pirate!",
            IsDefault = true
        };
        db.BrandVoicePrompts.Add(brandVoice);

        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft", Content = "Test content" };
        db.Drafts.Add(draft);

        await db.SaveChangesAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(db);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var probe = new ModelCapabilityProbe(null!, cache, NullLogger<ModelCapabilityProbe>.Instance);
        var providerService = new LlmProviderService();

        var loader = new ChatSessionLoader(
            scopeFactory,
            probe,
            null!,
            providerService
        );

        var session = await loader.LoadAsync(userId, draft.Id, null, new List<ChatModels.UiMessage>(), CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal("Arrr, write like a pirate!", session.DefaultBrandVoiceBody);
    }
}
