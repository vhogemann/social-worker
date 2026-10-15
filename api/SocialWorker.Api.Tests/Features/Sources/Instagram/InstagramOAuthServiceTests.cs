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
    public void GetAuthorizationUrl_Default_BuildsInstagramAuthorizeUrl()
    {
        var options = Options.Create(new InstagramOptions
        {
            AppId = "1122843966749115",
            RedirectUri = "https://social.example.com/oauth/callback/instagram"
        });

        var service = new InstagramOAuthService(new HttpClient(), options, NullLogger<InstagramOAuthService>.Instance);
        var url = service.GetAuthorizationUrl("nonce_123");

        Assert.StartsWith("https://api.instagram.com/oauth/authorize", url);
        Assert.Contains("client_id=1122843966749115", url);
        Assert.Contains("state=nonce_123", url);
        Assert.Contains("instagram_business_basic", url);
    }

    [Fact]
    public void GetAuthorizationUrl_WithCustomMetaDialog_BuildsMetaDialogUrl()
    {
        var options = Options.Create(new InstagramOptions
        {
            AppId = "123456",
            Scopes = "instagram_basic,pages_show_list",
            OAuthBaseUrl = "https://www.facebook.com/v20.0/dialog/oauth",
            RedirectUri = "https://social.example.com/oauth/callback/instagram"
        });

        var service = new InstagramOAuthService(new HttpClient(), options, NullLogger<InstagramOAuthService>.Instance);
        var url = service.GetAuthorizationUrl("nonce_123");

        Assert.StartsWith("https://www.facebook.com/v20.0/dialog/oauth", url);
        Assert.Contains("client_id=123456", url);
        Assert.Contains("state=nonce_123", url);
        Assert.Contains("instagram_basic", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithInstagramLogin_SendsPostWithFormContent()
    {
        HttpRequestMessage? captured = null;
        var json = """
        {
          "access_token": "ig_short_token",
          "user_id": 17841400123
        }
        """;

        var handler = new TestHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });

        var options = Options.Create(new InstagramOptions
        {
            AppId = "1122843966749115",
            AppSecret = "sec_123",
            OAuthBaseUrl = "https://api.instagram.com/oauth/authorize",
            RedirectUri = "https://social.example.com/cb"
        });

        var service = new InstagramOAuthService(new HttpClient(handler), options, NullLogger<InstagramOAuthService>.Instance);
        var result = await service.ExchangeCodeAsync("auth_code");

        Assert.Equal("ig_short_token", result.AccessToken);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("https://api.instagram.com/oauth/access_token", captured.RequestUri?.ToString());
    }

    [Fact]
    public async Task ExchangeForLongLivedTokenAsync_WithInstagramLogin_CallsIgExchangeTokenEndpoint()
    {
        HttpRequestMessage? captured = null;
        var json = """
        {
          "access_token": "ig_long_token",
          "token_type": "bearer",
          "expires_in": 5184000
        }
        """;

        var handler = new TestHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });

        var options = Options.Create(new InstagramOptions
        {
            AppId = "1122843966749115",
            AppSecret = "sec_123",
            OAuthBaseUrl = "https://api.instagram.com/oauth/authorize"
        });

        var service = new InstagramOAuthService(new HttpClient(handler), options, NullLogger<InstagramOAuthService>.Instance);
        var result = await service.ExchangeForLongLivedTokenAsync("short_token");

        Assert.Equal("ig_long_token", result.AccessToken);
        Assert.Equal(5184000, result.ExpiresIn);
        Assert.NotNull(captured);
        Assert.Contains("grant_type=ig_exchange_token", captured.RequestUri?.Query);
        Assert.Contains("graph.instagram.com", captured.RequestUri?.Host);
    }

    [Fact]
    public async Task DiscoverInstagramAccountAsync_WithInstagramLogin_ParsesMeDirectly()
    {
        var json = """
        {
          "id": "178414009999",
          "username": "sloppyjoebot"
        }
        """;

        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        var options = Options.Create(new InstagramOptions
        {
            OAuthBaseUrl = "https://api.instagram.com/oauth/authorize"
        });
        var service = new InstagramOAuthService(new HttpClient(handler), options, NullLogger<InstagramOAuthService>.Instance);

        var (igUserId, username, token) = await service.DiscoverInstagramAccountAsync("user_token");

        Assert.Equal("178414009999", igUserId);
        Assert.Equal("sloppyjoebot", username);
        Assert.Equal("user_token", token);
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
