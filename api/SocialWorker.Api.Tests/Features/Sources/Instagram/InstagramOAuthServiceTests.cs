using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources.Instagram;

public sealed class InstagramOAuthServiceTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public void GetAuthorizationUrl_BuildsMetaDialogUrl()
    {
        var options = Options.Create(new InstagramOptions
        {
            AppId = "123456",
            RedirectUri = "https://social.example.com/oauth/callback/instagram"
        });

        var service = new InstagramOAuthService(new HttpClient(), options, NullLogger<InstagramOAuthService>.Instance);
        var url = service.GetAuthorizationUrl("nonce_123");

        Assert.Contains("client_id=123456", url);
        Assert.Contains("state=nonce_123", url);
        Assert.Contains("instagram_basic", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ParsesTokenResponse()
    {
        var json = """
        {
          "access_token": "short_lived_token",
          "token_type": "Bearer",
          "expires_in": 3600
        }
        """;

        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        var options = Options.Create(new InstagramOptions
        {
            AppId = "123",
            AppSecret = "sec",
            RedirectUri = "https://social.example.com/cb"
        });

        var service = new InstagramOAuthService(new HttpClient(handler), options, NullLogger<InstagramOAuthService>.Instance);
        var result = await service.ExchangeCodeAsync("auth_code");

        Assert.Equal("short_lived_token", result.AccessToken);
    }

    [Fact]
    public async Task DiscoverInstagramAccountAsync_ExtractsLinkedBusinessAccount()
    {
        var json = """
        {
          "data": [
            {
              "id": "page_123",
              "name": "My Page",
              "access_token": "page_token",
              "instagram_business_account": {
                "id": "1784140099",
                "username": "senadorflavio",
                "name": "Flavio"
              }
            }
          ]
        }
        """;

        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        var options = Options.Create(new InstagramOptions());
        var service = new InstagramOAuthService(new HttpClient(handler), options, NullLogger<InstagramOAuthService>.Instance);

        var (igUserId, username, token) = await service.DiscoverInstagramAccountAsync("user_token");

        Assert.Equal("1784140099", igUserId);
        Assert.Equal("senadorflavio", username);
        Assert.Equal("page_token", token);
    }
}
