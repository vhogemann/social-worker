using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts;

public sealed class OAuthStateServiceTests : SqliteTestBase
{
    [Fact]
    public async Task IssueAsync_SavesStateAndReturnsNonce()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new OAuthStateService(db);

        var nonce = await service.IssueAsync(user.Id, "linkedin", "{\"Type\":\"personal\"}");

        Assert.NotNull(nonce);
        Assert.Equal(64, nonce.Length);

        var saved = await db.OAuthStates.FirstOrDefaultAsync(s => s.Nonce == nonce);
        Assert.NotNull(saved);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal("linkedin", saved.Provider);
        Assert.Equal("{\"Type\":\"personal\"}", saved.PayloadJson);
        Assert.True(saved.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ConsumeAsync_ReturnsStateAndDeletesFromDb()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new OAuthStateService(db);

        var nonce = await service.IssueAsync(user.Id, "linkedin", "{\"Type\":\"company\"}");

        var consumed = await service.ConsumeAsync(user.Id, "linkedin", nonce);
        Assert.NotNull(consumed);
        Assert.Equal("{\"Type\":\"company\"}", consumed.PayloadJson);

        var inDb = await db.OAuthStates.FirstOrDefaultAsync(s => s.Nonce == nonce);
        Assert.Null(inDb);

        var secondConsume = await service.ConsumeAsync(user.Id, "linkedin", nonce);
        Assert.Null(secondConsume);
    }

    [Fact]
    public async Task ConsumeAsync_WrongUserOrProvider_ReturnsNull()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new OAuthStateService(db);

        var nonce = await service.IssueAsync(user.Id, "linkedin");

        var wrongUser = await service.ConsumeAsync(Guid.NewGuid(), "linkedin", nonce);
        Assert.Null(wrongUser);

        var wrongProvider = await service.ConsumeAsync(user.Id, "metathreads", nonce);
        Assert.Null(wrongProvider);
    }

    [Fact]
    public async Task ConsumeAsync_ExpiredState_ReturnsNullAndDeletes()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new OAuthStateService(db);

        var nonce = await service.IssueAsync(user.Id, "linkedin", ttl: TimeSpan.FromSeconds(-10));

        var consumed = await service.ConsumeAsync(user.Id, "linkedin", nonce);
        Assert.Null(consumed);

        var inDb = await db.OAuthStates.FirstOrDefaultAsync(s => s.Nonce == nonce);
        Assert.Null(inDb);
    }

    [Fact]
    public async Task PruneExpiredAsync_RemovesOnlyExpired()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var service = new OAuthStateService(db);

        var validNonce = await service.IssueAsync(user.Id, "linkedin", ttl: TimeSpan.FromMinutes(10));
        var expiredNonce = await service.IssueAsync(user.Id, "linkedin", ttl: TimeSpan.FromMinutes(-10));

        var pruned = await service.PruneExpiredAsync();
        Assert.Equal(1, pruned);

        var valid = await db.OAuthStates.FirstOrDefaultAsync(s => s.Nonce == validNonce);
        Assert.NotNull(valid);

        var expired = await db.OAuthStates.FirstOrDefaultAsync(s => s.Nonce == expiredNonce);
        Assert.Null(expired);
    }
}
