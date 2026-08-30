using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Auth;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Auth;

public sealed class ApiKeyServiceTests : SqliteTestBase
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task CreateApiKeyAsync_GeneratesKeyWithPrefix_AndStoresHash()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new ApiKeyService(db, _cache);

        var result = await service.CreateApiKeyAsync(user.Id, "Antigravity CLI Key", "mcp,drafts");

        Assert.NotNull(result);
        Assert.StartsWith("sw_live_", result.PlaintextKey);
        Assert.Equal("Antigravity CLI Key", result.Name);
        Assert.Equal("mcp,drafts", result.Scopes);

        var savedInDb = await db.ApiKeys.FindAsync(result.Id);
        Assert.NotNull(savedInDb);
        Assert.NotEqual(result.PlaintextKey, savedInDb!.KeyHash);
        Assert.Equal(ApiKeyService.ComputeSha256Hash(result.PlaintextKey), savedInDb.KeyHash);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_SucceedsForValidKey_UpdatesLastUsedAt()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new ApiKeyService(db, _cache);

        var created = await service.CreateApiKeyAsync(user.Id, "Test Key");
        var validated = await service.ValidateApiKeyAsync(created.PlaintextKey);

        Assert.NotNull(validated);
        Assert.Equal(user.Id, validated!.UserId);
        Assert.NotNull(validated.LastUsedAt);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_AcceptsBearerPrefix()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new ApiKeyService(db, _cache);

        var created = await service.CreateApiKeyAsync(user.Id, "Bearer Test Key");
        var validated = await service.ValidateApiKeyAsync($"Bearer {created.PlaintextKey}");

        Assert.NotNull(validated);
        Assert.Equal(user.Id, validated!.UserId);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ReturnsNullForRevokedOrExpiredKey()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new ApiKeyService(db, _cache);

        var created = await service.CreateApiKeyAsync(user.Id, "Revoked Key");
        await service.RevokeApiKeyAsync(user.Id, created.Id);

        var validated = await service.ValidateApiKeyAsync(created.PlaintextKey);
        Assert.Null(validated);

        // Expired key
        var expiredResult = await service.CreateApiKeyAsync(user.Id, "Expired Key", expiresAt: DateTime.UtcNow.AddMinutes(-5));
        var validatedExpired = await service.ValidateApiKeyAsync(expiredResult.PlaintextKey);
        Assert.Null(validatedExpired);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ReturnsNullForInvalidOrNonSwKeys()
    {
        using var db = CreateDbContext();
        var service = new ApiKeyService(db, _cache);

        Assert.Null(await service.ValidateApiKeyAsync("invalid-key"));
        Assert.Null(await service.ValidateApiKeyAsync(null));
        Assert.Null(await service.ValidateApiKeyAsync(""));
    }
}
