using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts.Tokens;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts.Tokens;

public sealed class TokenRefreshHostedServiceTests : SqliteTestBase
{
    private sealed class StubRefresher : ITokenRefresher
    {
        public string Platform => "MetaThreads";
        public List<Guid> RefreshedAccountIds { get; } = new();

        public Task<bool> RefreshAccountTokenAsync(Account account, CancellationToken ct = default)
        {
            RefreshedAccountIds.Add(account.Id);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task CheckAndRefreshTokensAsync_RefreshesOnlyExpiringAccounts()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var expiringAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "expiring_user",
            TokenExpiresAt = DateTime.UtcNow.AddDays(3),
            Status = "Active"
        };
        var notExpiringAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "safe_user",
            TokenExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = "Active"
        };
        db.Accounts.AddRange(expiringAccount, notExpiringAccount);
        await db.SaveChangesAsync();

        var stubRefresher = new StubRefresher();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<ITokenRefresher>(stubRefresher);
        var provider = services.BuildServiceProvider();

        var hostedService = new TokenRefreshHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshHostedService>.Instance);

        await hostedService.CheckAndRefreshTokensAsync();

        Assert.Single(stubRefresher.RefreshedAccountIds);
        Assert.Equal(expiringAccount.Id, stubRefresher.RefreshedAccountIds[0]);
    }
}
