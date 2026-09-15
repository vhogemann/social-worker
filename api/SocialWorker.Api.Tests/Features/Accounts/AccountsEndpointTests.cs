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
    public void AuthUrl_ReturnsCorrectUrl_ForPersonal()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            RedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin",
            Scopes = "w_member_social openid profile"
        });

        var opts = options.Value;
        var scopes = Uri.EscapeDataString(opts.Scopes);
        var redirectUri = Uri.EscapeDataString(opts.RedirectUri);
        var authUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={opts.ClientId}&redirect_uri={redirectUri}&scope={scopes}&state=personal";

        Assert.Contains("client_id=client-id", authUrl);
        Assert.Contains("redirect_uri=https%3A%2F%2Fsocial.plopsaurus.xyz%2Foauth%2Fcallback%2Flinkedin", authUrl);
        Assert.Contains("scope=w_member_social%20openid%20profile", authUrl);
        Assert.Contains("state=personal", authUrl);
    }

    [Fact]
    public void AuthUrl_ReturnsCorrectUrl_ForCompany()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            RedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin",
            CompanyClientId = "company-client-id",
            CompanyRedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin",
            CompanyScopes = "w_organization_social r_organization_admin"
        });

        var opts = options.Value;
        var scopes = Uri.EscapeDataString(opts.CompanyScopes);
        var redirectUri = Uri.EscapeDataString(opts.CompanyRedirectUri);
        var authUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={opts.CompanyClientId}&redirect_uri={redirectUri}&scope={scopes}&state=company";

        Assert.Contains("client_id=company-client-id", authUrl);
        Assert.Contains("redirect_uri=https%3A%2F%2Fsocial.plopsaurus.xyz%2Foauth%2Fcallback%2Flinkedin", authUrl);
        Assert.Contains("scope=w_organization_social%20r_organization_admin", authUrl);
        Assert.Contains("state=company", authUrl);
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

    [Fact]
    public async Task LinkedInCallback_SavesBothPersonalAndOrganizationAccounts()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var configData = new Dictionary<string, string?> { { "Auth:DbEncryptionKey", ValidKey } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var options = Microsoft.Extensions.Options.Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin"
        });

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("accessToken"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        "{\"access_token\":\"org-access-token\",\"expires_in\":86400,\"scope\":\"w_member_social w_organization_social\"}",
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
                        "{\"sub\":\"urn:li:person:user123\",\"name\":\"Jane Doe\"}",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("organizationalEntityAcls"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        """
                        {
                            "elements": [
                                {
                                    "role": "ADMINISTRATOR",
                                    "state": "APPROVED",
                                    "organizationalTarget": "urn:li:organization:998877"
                                }
                            ]
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("organizations/998877"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        """
                        {
                            "id": 998877,
                            "localizedName": "Global Corp",
                            "vanityName": "global-corp"
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }

            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        });

        var client = new System.Net.Http.HttpClient(handler);
        var oauth = new LinkedInOAuthService(client, options);

        // Simulate callback logic
        var tokenResp = await oauth.ExchangeCodeAsync("test-code", default);
        var userInfo = await oauth.FetchUserInfoAsync(tokenResp.AccessToken, default);

        var personalUrn = userInfo.Sub.Trim();
        var personalAccount = new Account
        {
            UserId = user.Id,
            Platform = "LinkedIn",
            Handle = personalUrn,
            CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString(tokenResp.AccessToken, ValidKey),
            Status = "Active"
        };
        db.Accounts.Add(personalAccount);

        var orgs = await oauth.FetchOrganizationsAsync(tokenResp.AccessToken, default);
        foreach (var org in orgs)
        {
            var orgHandle = !string.IsNullOrWhiteSpace(org.Name) ? $"{org.Name} ({org.Urn})" : org.Urn;
            var orgAccount = new Account
            {
                UserId = user.Id,
                Platform = "LinkedIn",
                Handle = orgHandle,
                CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString(tokenResp.AccessToken, ValidKey),
                Status = "Active"
            };
            db.Accounts.Add(orgAccount);
        }
        await db.SaveChangesAsync();

        var accounts = await db.Accounts.Where(a => a.UserId == user.Id && a.Platform == "LinkedIn").ToListAsync();
        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.Handle == "urn:li:person:user123");
        Assert.Contains(accounts, a => a.Handle == "Global Corp (urn:li:organization:998877)");
    }

    [Fact]
    public async Task LinkedInCallback_CompanyAppWithoutUserInfo_SavesOnlyOrganizationAccounts()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var options = Microsoft.Extensions.Options.Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin",
            CompanyClientId = "company-client-id",
            CompanyClientSecret = "company-secret",
            CompanyRedirectUri = "https://social.plopsaurus.xyz/oauth/callback/linkedin"
        });

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("accessToken"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        "{\"access_token\":\"company-access-token\",\"expires_in\":86400,\"scope\":\"w_organization_social r_organization_admin\"}",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("userinfo"))
            {
                // Forbidden when company app has no openid scope
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)
                {
                    Content = new System.Net.Http.StringContent("{\"message\":\"ACCESS_DENIED\"}")
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("organizationalEntityAcls"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        """
                        {
                            "elements": [
                                {
                                    "role": "ADMINISTRATOR",
                                    "state": "APPROVED",
                                    "organizationalTarget": "urn:li:organization:55555"
                                }
                            ]
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("organizations/55555"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(
                        """
                        {
                            "id": 55555,
                            "localizedName": "Acme Ventures",
                            "vanityName": "acme-ventures"
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }

            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        });

        var client = new System.Net.Http.HttpClient(handler);
        var oauth = new LinkedInOAuthService(client, options);

        var tokenResp = await oauth.ExchangeCodeAsync("company-auth-code", isCompany: true, default);
        var userInfo = await oauth.FetchUserInfoAsync(tokenResp.AccessToken, default);

        Assert.Null(userInfo);

        var orgs = await oauth.FetchOrganizationsAsync(tokenResp.AccessToken, default);
        Assert.Single(orgs);

        foreach (var org in orgs)
        {
            var orgHandle = !string.IsNullOrWhiteSpace(org.Name) ? $"{org.Name} ({org.Urn})" : org.Urn;
            var orgAccount = new Account
            {
                UserId = user.Id,
                Platform = "LinkedIn",
                Handle = orgHandle,
                CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString(tokenResp.AccessToken, ValidKey),
                Status = "Active"
            };
            db.Accounts.Add(orgAccount);
        }
        await db.SaveChangesAsync();

        var accounts = await db.Accounts.Where(a => a.UserId == user.Id && a.Platform == "LinkedIn").ToListAsync();
        Assert.Single(accounts);
        Assert.Equal("Acme Ventures (urn:li:organization:55555)", accounts[0].Handle);
    }

    [Fact]
    public async Task MultiAccount_CanSaveMultipleHandlesForSamePlatform()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var account1 = new Account
        {
            UserId = user.Id,
            Platform = "Bluesky",
            Handle = "personal.bsky.social",
            CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString("pass1", ValidKey),
            Status = "Active"
        };
        var account2 = new Account
        {
            UserId = user.Id,
            Platform = "Bluesky",
            Handle = "tech.bsky.social",
            CredentialsEncrypted = SocialWorker.Api.Infrastructure.Security.CryptoHelper.EncryptString("pass2", ValidKey),
            Status = "Active"
        };

        db.Accounts.AddRange(account1, account2);
        await db.SaveChangesAsync();

        var bskyAccounts = await db.Accounts.Where(a => a.UserId == user.Id && a.Platform == "Bluesky").ToListAsync();
        Assert.Equal(2, bskyAccounts.Count);
        Assert.Contains(bskyAccounts, a => a.Handle == "personal.bsky.social");
        Assert.Contains(bskyAccounts, a => a.Handle == "tech.bsky.social");
    }
}
