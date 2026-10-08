using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts.Tokens;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts.Tokens;

public sealed class TwitterTokenRefresherTests : SqliteTestBase
{
    private const string ValidKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task RefreshAccountTokenAsync_RefreshesAndRotatesCredentials()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var initialCreds = new TwitterAccountCredentials(
            AccessToken: "OLD_ACCESS_TOKEN",
            RefreshToken: "OLD_REFRESH_TOKEN",
            TokenType: "bearer",
            Scope: "tweet.read tweet.write");
        var encryptedToken = CryptoHelper.EncryptString(JsonSerializer.Serialize(initialCreds), ValidKey);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Twitter",
            Handle = "alice_dev",
            ExternalId = "987654321",
            CredentialsEncrypted = encryptedToken,
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new DelegatingHandlerStub(req =>
        {
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("grant_type=refresh_token", body);
            Assert.Contains("refresh_token=OLD_REFRESH_TOKEN", body);

            var resp = new TwitterTokenResponse(
                TokenType: "bearer",
                ExpiresIn: 7200,
                AccessToken: "NEW_ACCESS_TOKEN",
                Scope: "tweet.read tweet.write",
                RefreshToken: "NEW_ROTATED_REFRESH_TOKEN");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        });

        var options = new TwitterOptions
        {
            ClientId = "client_123",
            ClientSecret = "secret_abc"
        };
        var oauth = new TwitterOAuthService(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options), NullLogger<TwitterOAuthService>.Instance);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = ValidKey
            })
            .Build();

        var refresher = new TwitterTokenRefresher(oauth, db, config, NullLogger<TwitterTokenRefresher>.Instance);

        var success = await refresher.RefreshAccountTokenAsync(account);

        Assert.True(success);
        Assert.Equal("Active", account.Status);
        Assert.True(account.TokenExpiresAt > DateTime.UtcNow.AddHours(1));

        var decryptedJson = CryptoHelper.DecryptString(account.CredentialsEncrypted, ValidKey);
        var updatedCreds = JsonSerializer.Deserialize<TwitterAccountCredentials>(decryptedJson);
        Assert.NotNull(updatedCreds);
        Assert.Equal("NEW_ACCESS_TOKEN", updatedCreds.AccessToken);
        Assert.Equal("NEW_ROTATED_REFRESH_TOKEN", updatedCreds.RefreshToken);
    }

    [Fact]
    public async Task RefreshAccountTokenAsync_WhenHttpFails_MarksAccountInvalid()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var initialCreds = new TwitterAccountCredentials(
            AccessToken: "OLD_ACCESS_TOKEN",
            RefreshToken: "REVOKED_REFRESH_TOKEN");
        var encryptedToken = CryptoHelper.EncryptString(JsonSerializer.Serialize(initialCreds), ValidKey);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Twitter",
            Handle = "alice_dev",
            ExternalId = "987654321",
            CredentialsEncrypted = encryptedToken,
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new DelegatingHandlerStub(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"Value passed for the token was invalid.\"}", Encoding.UTF8, "application/json")
            };
        });

        var options = new TwitterOptions { ClientId = "client_123" };
        var oauth = new TwitterOAuthService(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options), NullLogger<TwitterOAuthService>.Instance);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = ValidKey
            })
            .Build();

        var refresher = new TwitterTokenRefresher(oauth, db, config, NullLogger<TwitterTokenRefresher>.Instance);

        var success = await refresher.RefreshAccountTokenAsync(account);

        Assert.False(success);
        Assert.Equal("Invalid", account.Status);
    }

    [Fact]
    public async Task RefreshAccountTokenAsync_MissingRefreshToken_MarksAccountInvalid()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var initialCreds = new TwitterAccountCredentials(
            AccessToken: "ACCESS_ONLY",
            RefreshToken: null);
        var encryptedToken = CryptoHelper.EncryptString(JsonSerializer.Serialize(initialCreds), ValidKey);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Twitter",
            Handle = "alice_dev",
            ExternalId = "987654321",
            CredentialsEncrypted = encryptedToken,
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var options = new TwitterOptions { ClientId = "client_123" };
        var oauth = new TwitterOAuthService(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options), NullLogger<TwitterOAuthService>.Instance);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DbEncryptionKey"] = ValidKey
            })
            .Build();

        var refresher = new TwitterTokenRefresher(oauth, db, config, NullLogger<TwitterTokenRefresher>.Instance);

        var success = await refresher.RefreshAccountTokenAsync(account);

        Assert.False(success);
        Assert.Equal("Invalid", account.Status);
    }
}
