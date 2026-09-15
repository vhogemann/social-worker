using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Accounts;

public sealed class LinkedInOAuthServiceTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _sender;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sender)
        {
            _sender = sender;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_sender(request));
        }
    }

    [Fact]
    public async Task ExchangeCodeAsync_SucceedsAndReturnsTokens()
    {
        var options = Options.Create(new LinkedInOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "http://localhost/callback"
        });

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("https://www.linkedin.com/oauth/v2/accessToken", req.RequestUri!.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"my-token\",\"expires_in\":86400,\"scope\":\"w_member_social\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            };
        });

        var client = new HttpClient(handler);
        var service = new LinkedInOAuthService(client, options);

        var result = await service.ExchangeCodeAsync("auth-code", CancellationToken.None);

        Assert.Equal("my-token", result.AccessToken);
        Assert.Equal(86400, result.ExpiresIn);
        Assert.Equal("w_member_social", result.Scope);
    }

    [Fact]
    public async Task FetchUserInfoAsync_SucceedsAndReturnsProfile()
    {
        var options = Options.Create(new LinkedInOptions());
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("https://api.linkedin.com/v2/userinfo", req.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer my-token", req.Headers.Authorization!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"sub\":\"urn:li:person:abcdef\",\"name\":\"Jane Doe\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            };
        });

        var client = new HttpClient(handler);
        var service = new LinkedInOAuthService(client, options);

        var result = await service.FetchUserInfoAsync("my-token", CancellationToken.None);

        Assert.Equal("urn:li:person:abcdef", result.Sub);
        Assert.Equal("Jane Doe", result.Name);
    }

    [Fact]
    public async Task FetchOrganizationsAsync_ReturnsOrganizations_WhenAclAndDetailsSucceed()
    {
        var options = Options.Create(new LinkedInOptions());
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("organizationalEntityAcls"))
            {
                Assert.Equal("Bearer my-token", req.Headers.Authorization!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                            "elements": [
                                {
                                    "role": "ADMINISTRATOR",
                                    "state": "APPROVED",
                                    "organizationalTarget": "urn:li:organization:12345"
                                },
                                {
                                    "role": "ANALYST",
                                    "state": "APPROVED",
                                    "organizationalTarget": "urn:li:organization:99999"
                                },
                                {
                                    "role": "ADMINISTRATOR",
                                    "state": "PENDING",
                                    "organizationalTarget": "urn:li:organization:88888"
                                }
                            ]
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }
            if (req.RequestUri!.AbsoluteUri.Contains("organizations/12345"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                            "id": 12345,
                            "localizedName": "Acme Corp",
                            "vanityName": "acme-corp"
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var service = new LinkedInOAuthService(client, options);

        var result = await service.FetchOrganizationsAsync("my-token", CancellationToken.None);

        Assert.Single(result);
        var org = result[0];
        Assert.Equal("urn:li:organization:12345", org.Urn);
        Assert.Equal("Acme Corp", org.Name);
        Assert.Equal("acme-corp", org.VanityName);
        Assert.Equal("ADMINISTRATOR", org.Role);
    }

    [Fact]
    public async Task FetchOrganizationsAsync_ReturnsEmpty_WhenAclFailsOrUnauthorized()
    {
        var options = Options.Create(new LinkedInOptions());
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"message\":\"Not enough permissions\"}")
            };
        });

        var client = new HttpClient(handler);
        var service = new LinkedInOAuthService(client, options);

        var result = await service.FetchOrganizationsAsync("my-token", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithIsCompanyTrue_UsesCompanyCredentials()
    {
        var options = Options.Create(new LinkedInOptions
        {
            ClientId = "personal-client-id",
            ClientSecret = "personal-secret",
            RedirectUri = "http://localhost/callback",
            CompanyClientId = "company-client-id",
            CompanyClientSecret = "company-secret",
            CompanyRedirectUri = "http://localhost/callback-company"
        });

        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"company-token\",\"expires_in\":86400,\"scope\":\"w_organization_social\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            };
        });

        var client = new HttpClient(handler);
        var service = new LinkedInOAuthService(client, options);

        var result = await service.ExchangeCodeAsync("company-code", isCompany: true, CancellationToken.None);

        Assert.Equal("company-token", result.AccessToken);
        Assert.Contains("client_id=company-client-id", capturedBody);
        Assert.Contains("client_secret=company-secret", capturedBody);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%2Fcallback-company", capturedBody);
    }

    [Fact]
    public async Task FetchUserInfoAsync_ReturnsNull_WhenForbidden()
    {
        var options = Options.Create(new LinkedInOptions());
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"message\":\"ACCESS_DENIED\"}")
            };
        });

        var client = new HttpClient(handler);
        var service = new LinkedInOAuthService(client, options);

        var result = await service.FetchUserInfoAsync("company-token", CancellationToken.None);

        Assert.Null(result);
    }
}
