using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Tests;

public sealed class LlmProviderTests : SqliteTestBase
{
    [Fact]
    public async Task Can_Insert_And_Retrieve_LlmProvider()
    {
        using var db = new AppDbContext(Options);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "OpenRouter Claude",
            ProviderType = "OpenRouter",
            BaseUrl = "https://openrouter.ai/api/v1",
            ApiKey = "sk-or-1234",
            Model = "anthropic/claude-3",
            IsDefault = true,
            IsActive = true
        };

        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var retrieved = await db.LlmProviders.FirstOrDefaultAsync(p => p.Id == provider.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("OpenRouter Claude", retrieved.Name);
        Assert.Equal("OpenRouter", retrieved.ProviderType);
        Assert.Equal("https://openrouter.ai/api/v1", retrieved.BaseUrl);
        Assert.Equal("sk-or-1234", retrieved.ApiKey);
        Assert.Equal("anthropic/claude-3", retrieved.Model);
        Assert.True(retrieved.IsDefault);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public async Task AppUser_Can_Have_PreferredProvider()
    {
        using var db = new AppDbContext(Options);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Ollama Local",
            ProviderType = "Ollama",
            BaseUrl = "http://localhost:11434/v1",
            ApiKey = "",
            Model = "llama3",
            IsDefault = false,
            IsActive = true
        };

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hashed",
            Role = "User",
            IsActive = true,
            PreferredProviderId = provider.Id
        };

        db.LlmProviders.Add(provider);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var retrievedUser = await db.Users
            .Include(u => u.PreferredProvider)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        Assert.NotNull(retrievedUser);
        Assert.Equal(provider.Id, retrievedUser.PreferredProviderId);
        Assert.NotNull(retrievedUser.PreferredProvider);
        Assert.Equal("Ollama Local", retrievedUser.PreferredProvider.Name);
    }

    [Fact]
    public async Task Deleting_LlmProvider_Sets_PreferredProviderId_To_Null()
    {
        using var db = new AppDbContext(Options);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Temporary Provider",
            ProviderType = "OpenRouter",
            BaseUrl = "https://example.com",
            ApiKey = "key",
            Model = "gpt-4",
            IsDefault = false,
            IsActive = true
        };

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            Email = "user2@example.com",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            PreferredProviderId = provider.Id
        };

        db.LlmProviders.Add(provider);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Delete the provider
        db.LlmProviders.Remove(provider);
        await db.SaveChangesAsync();

        var retrievedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(retrievedUser);
        Assert.Null(retrievedUser.PreferredProviderId);
    }
}
