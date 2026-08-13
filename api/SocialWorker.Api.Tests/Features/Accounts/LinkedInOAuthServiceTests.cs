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
}
