using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources.Instagram;
using SocialWorker.Api.Infrastructure.Security;
using SocialWorker.Api.Tests;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources.Instagram;

public sealed class InstagramCredentialProviderTests : SqliteTestBase
{
    private const string Base64Key = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class FakeInstagramDiscoveryClient : IInstagramDiscoveryClient
    {
        public string? ResolvedId { get; set; }

        public Task<InstagramProfileDiscovery?> GetProfileWithMediaAsync(string callerIgUserId, string targetUsername, string accessToken, int limit = 10, CancellationToken ct = default)
        {
            return Task.FromResult<InstagramProfileDiscovery?>(null);
        }

        public Task<string?> ResolveCallerIgUserIdAsync(string accessToken, CancellationToken ct = default)
        {
            return Task.FromResult(ResolvedId);
        }

        public Task<Feed> GetProfileFeedAsync(string targetUsername, string callerIgUserId, string accessToken, int limit = 10, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_FromDatabaseAccount_DecryptsTokenAndReturns()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var plainToken = "EAAB1234567890abcdef";
        var encrypted = CryptoHelper.EncryptString(plainToken, Base64Key);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Instagram",
            Handle = "@mycreator",
            ExternalId = "1784149999",
            CredentialsEncrypted = encrypted,
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var client = new FakeInstagramDiscoveryClient();
        var options = Microsoft.Extensions.Options.Options.Create(new InstagramOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = Base64Key
            })
            .Build();

        var provider = new InstagramCredentialProvider(
            db,
            client,
            options,
            config,
            NullLogger<InstagramCredentialProvider>.Instance);

        var creds = await provider.GetCredentialsAsync(user.Id);

        Assert.NotNull(creds);
        Assert.Equal(plainToken, creds.AccessToken);
        Assert.Equal("1784149999", creds.CallerIgUserId);
    }

    [Fact]
    public async Task GetCredentialsAsync_WhenExternalIdMissing_AutoResolvesAndSaves()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var plainToken = "EAAB1234567890abcdef";
        var encrypted = CryptoHelper.EncryptString(plainToken, Base64Key);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Instagram",
            Handle = "@mycreator",
            ExternalId = null,
            CredentialsEncrypted = encrypted,
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var client = new FakeInstagramDiscoveryClient { ResolvedId = "1784149999" };
        var options = Microsoft.Extensions.Options.Options.Create(new InstagramOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = Base64Key
            })
            .Build();

        var provider = new InstagramCredentialProvider(
            db,
            client,
            options,
            config,
            NullLogger<InstagramCredentialProvider>.Instance);

        var creds = await provider.GetCredentialsAsync(user.Id);

        Assert.NotNull(creds);
        Assert.Equal("1784149999", creds.CallerIgUserId);

        var updatedAccount = await db.Accounts.FindAsync(account.Id);
        Assert.Equal("1784149999", updatedAccount?.ExternalId);
    }

    [Fact]
    public async Task GetCredentialsAsync_FallsBackToOptions_WhenNoDatabaseAccount()
    {
        using var db = CreateDbContext();
        var client = new FakeInstagramDiscoveryClient();
        var options = Microsoft.Extensions.Options.Options.Create(new InstagramOptions
        {
            AccessToken = "env_token",
            CallerUserId = "1784145555"
        });
        var config = new ConfigurationBuilder().Build();

        var provider = new InstagramCredentialProvider(
            db,
            client,
            options,
            config,
            NullLogger<InstagramCredentialProvider>.Instance);

        var creds = await provider.GetCredentialsAsync(null);

        Assert.NotNull(creds);
        Assert.Equal("env_token", creds.AccessToken);
        Assert.Equal("1784145555", creds.CallerIgUserId);
    }
}
