using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts.Tokens;
using SocialWorker.Api.Features.Sources.Instagram;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts.Tokens;

public sealed class InstagramTokenRefresherTests : SqliteTestBase
{
    private const string EncryptionKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    [Fact]
    public async Task RefreshAccountTokenAsync_UpdatesEncryptedTokenAndExpiry()
    {
        var db = CreateDbContext();
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Contains("graph.instagram.com/refresh_access_token", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"new_long_token\",\"token_type\":\"bearer\",\"expires_in\":5184000}")
            };
        });
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(new InstagramOptions());
        var oauth = new InstagramOAuthService(httpClient, opts, NullLogger<InstagramOAuthService>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:DbEncryptionKey"] = EncryptionKey })
            .Build();

        var refresher = new InstagramTokenRefresher(oauth, db, config, NullLogger<InstagramTokenRefresher>.Instance);

        var user = CreateSeedUser(db);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Instagram",
            Handle = "@creator",
            CredentialsEncrypted = CryptoHelper.EncryptString("old_token", EncryptionKey),
            TokenExpiresAt = DateTime.UtcNow.AddDays(5)
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var result = await refresher.RefreshAccountTokenAsync(account);

        Assert.True(result);
        Assert.Equal("new_long_token", CryptoHelper.DecryptString(account.CredentialsEncrypted, EncryptionKey));
        Assert.True(account.TokenExpiresAt > DateTime.UtcNow.AddDays(50));
    }

    [Fact]
    public async Task RefreshAccountTokenAsync_OnHttpError_ReturnsFalse()
    {
        var db = CreateDbContext();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Token expired\"}}")
        });
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(new InstagramOptions());
        var oauth = new InstagramOAuthService(httpClient, opts, NullLogger<InstagramOAuthService>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:DbEncryptionKey"] = EncryptionKey })
            .Build();

        var refresher = new InstagramTokenRefresher(oauth, db, config, NullLogger<InstagramTokenRefresher>.Instance);

        var user = CreateSeedUser(db, username: "creator2", email: "creator2@example.com");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Instagram",
            Handle = "@creator",
            CredentialsEncrypted = CryptoHelper.EncryptString("old_token", EncryptionKey)
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var result = await refresher.RefreshAccountTokenAsync(account);

        Assert.False(result);
    }
}
