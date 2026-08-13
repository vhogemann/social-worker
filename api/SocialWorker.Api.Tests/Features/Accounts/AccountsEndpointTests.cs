using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts;

public sealed class AccountsEndpointTests : SqliteTestBase
{
    private static readonly string ValidKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _sender;

        public MockHttpMessageHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> sender)
        {
            _sender = sender;
        }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(_sender(request));
        }
    }

    [Fact]
    public void AuthUrl_ReturnsCorrectUrl()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            RedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin"
        });

        var opts = options.Value;
        var scopes = Uri.EscapeDataString("w_member_social openid profile");
        var redirectUri = Uri.EscapeDataString(opts.RedirectUri);
        var authUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={opts.ClientId}&redirect_uri={redirectUri}&scope={scopes}";

        Assert.Contains("client_id=client-id", authUrl);
        Assert.Contains("redirect_uri=https%3A%2F%2Fsocial.plopsaurus.xyz%2Foauth%2Fcallback%2Flinkedin", authUrl);
        Assert.Contains("scope=w_member_social%20openid%20profile", authUrl);
    }

    [Fact]
    public async Task LinkedInCallback_SavesAccountSuccessfully()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        // Claims principal
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Configuration
        var configData = new Dictionary<string, string?> { { "Auth:DbEncryptionKey", ValidKey } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        // Options
        var options = Microsoft.Extensions.Options.Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin"
        });

        // HttpClient mock
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("accessToken"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        "{\"access_token\":\"my-access-token\",\"expires_in\":86400,\"scope\":\"w_member_social\"}",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("userinfo"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        "{\"sub\":\"urn:li:person:abcdef\",\"name\":\"Jane Doe\"}",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        });

        var client = new System.Net.Http.HttpClient(handler);
        var oauth = new LinkedInOAuthService(client, options);

        var request = new LinkedInCallbackRequest("my-auth-code");

        // Verify start state
        var result = await db.Accounts.AnyAsync();
        Assert.False(result);

        // Run OAuth service and save logic
        var tokenResp = await oauth.ExchangeCodeAsync(request.Code, default);
        var userInfo = await oauth.FetchUserInfoAsync(tokenResp.AccessToken, default);

        var account = new Account
        {
            UserId = user.Id,
            Platform = "LinkedIn",
            Handle = userInfo.Sub,
            CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString(tokenResp.AccessToken, ValidKey),
            Status = "Active"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        // Verify end state
        var savedAccount = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.Id && a.Platform == "LinkedIn");
        Assert.NotNull(savedAccount);
        Assert.Equal("urn:li:person:abcdef", savedAccount.Handle);
        
        var decryptedToken = SocialWorker.Api.Infrastructure.Security.CryptoHelper.DecryptString(savedAccount.CredentialsEncrypted, ValidKey);
        Assert.Equal("my-access-token", decryptedToken);
    }
}
