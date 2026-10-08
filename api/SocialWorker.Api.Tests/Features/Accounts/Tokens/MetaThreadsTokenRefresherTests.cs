using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts.Tokens;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts.Tokens;

public sealed class MetaThreadsTokenRefresherTests : SqliteTestBase
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
    public async Task RefreshAccountTokenAsync_RefreshesAndUpdatesExpiry()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var encryptedToken = CryptoHelper.EncryptString("OLD_LONG_TOKEN", ValidKey);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "threads_user",
            ExternalId = "123456",
            CredentialsEncrypted = encryptedToken,
            TokenExpiresAt = DateTime.UtcNow.AddDays(2),
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new DelegatingHandlerStub(req =>
        {
            Assert.Contains("grant_type=th_refresh_token", req.RequestUri!.Query);
            Assert.Contains("access_token=OLD_LONG_TOKEN", req.RequestUri.Query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"NEW_LONG_TOKEN\",\"token_type\":\"bearer\",\"expires_in\":5184000}")
            };
        });

        var options = new MetaThreadsOptions { AppId = "app1", AppSecret = "secret1" };
        var oauth = new MetaThreadsOAuthService(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options), NullLogger<MetaThreadsOAuthService>.Instance);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:DbEncryptionKey"] = ValidKey
        }).Build();

        var refresher = new MetaThreadsTokenRefresher(oauth, db, config, NullLogger<MetaThreadsTokenRefresher>.Instance);

        var success = await refresher.RefreshAccountTokenAsync(account);

        Assert.True(success);
        var updated = await db.Accounts.FindAsync(account.Id);
        Assert.NotNull(updated);

        var decrypted = CryptoHelper.DecryptString(updated.CredentialsEncrypted, ValidKey);
        Assert.Equal("NEW_LONG_TOKEN", decrypted);
        Assert.True(updated.TokenExpiresAt > DateTime.UtcNow.AddDays(50));
    }
}
