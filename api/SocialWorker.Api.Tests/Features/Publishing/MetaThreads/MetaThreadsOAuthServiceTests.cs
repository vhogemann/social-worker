using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.MetaThreads;

public sealed class MetaThreadsOAuthServiceTests
{
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

    private static (MetaThreadsOAuthService Service, MetaThreadsOptions Options) CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var options = new MetaThreadsOptions
        {
            AppId = "123456",
            AppSecret = "secret789",
            RedirectUri = "https://app.social-worker.local/oauth/callback/threads",
            Scopes = "threads_basic,threads_content_publish,threads_manage_replies"
        };
        var http = new HttpClient(new DelegatingHandlerStub(handler));
        var service = new MetaThreadsOAuthService(http, Options.Create(options), NullLogger<MetaThreadsOAuthService>.Instance);
        return (service, options);
    }

    [Fact]
    public void GetAuthorizationUrl_BuildsValidUrl()
    {
        var (service, opts) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var url = service.GetAuthorizationUrl("test_nonce_123");

        Assert.Contains("client_id=123456", url);
        Assert.Contains("state=test_nonce_123", url);
        Assert.Contains("scope=threads_basic%2Cthreads_content_publish%2Cthreads_manage_replies", url);
        Assert.Contains("response_type=code", url);
        Assert.StartsWith("https://threads.net/oauth/authorize", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsTokenResponse()
    {
        var (service, _) = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://graph.threads.net/oauth/access_token", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"TH_SHORT_123\",\"user_id\":\"user_999\"}")
            };
        });

        var result = await service.ExchangeCodeAsync("auth_code_xyz");

        Assert.Equal("TH_SHORT_123", result.AccessToken);
        Assert.Equal("user_999", result.UserId);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsTokenResponse_WhenUserIdIsNumeric()
    {
        var (service, _) = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://graph.threads.net/oauth/access_token", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"TH_SHORT_123\",\"user_id\":1585440656142454}")
            };
        });

        var result = await service.ExchangeCodeAsync("auth_code_xyz");

        Assert.Equal("TH_SHORT_123", result.AccessToken);
        Assert.Equal("1585440656142454", result.UserId);
    }

    [Fact]
    public async Task ExchangeForLongLivedTokenAsync_ReturnsLongLivedToken()
    {
        var (service, _) = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("grant_type=th_exchange_token", req.RequestUri!.Query);
            Assert.Contains("client_secret=secret789", req.RequestUri.Query);
            Assert.Contains("access_token=TH_SHORT_123", req.RequestUri.Query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"TH_LONG_456\",\"token_type\":\"bearer\",\"expires_in\":5184000}")
            };
        });

        var result = await service.ExchangeForLongLivedTokenAsync("TH_SHORT_123");

        Assert.Equal("TH_LONG_456", result.AccessToken);
        Assert.Equal(5184000, result.ExpiresIn);
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsRefreshedToken()
    {
        var (service, _) = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("grant_type=th_refresh_token", req.RequestUri!.Query);
            Assert.Contains("access_token=TH_LONG_OLD", req.RequestUri.Query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"TH_LONG_NEW\",\"token_type\":\"bearer\",\"expires_in\":5184000}")
            };
        });

        var result = await service.RefreshTokenAsync("TH_LONG_OLD");

        Assert.Equal("TH_LONG_NEW", result.AccessToken);
        Assert.Equal(5184000, result.ExpiresIn);
    }

    [Fact]
    public async Task FetchUserProfileAsync_ReturnsUserProfile()
    {
        var (service, _) = CreateService(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("fields=id%2Cusername%2Cthreads_profile_picture_url", req.RequestUri!.Query.Replace(",", "%2C"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"user_999\",\"username\":\"threads_tester\",\"threads_profile_picture_url\":\"https://example.com/pic.jpg\"}")
            };
        });

        var profile = await service.FetchUserProfileAsync("TH_TOKEN");

        Assert.Equal("user_999", profile.Id);
        Assert.Equal("threads_tester", profile.Username);
        Assert.Equal("https://example.com/pic.jpg", profile.ProfilePictureUrl);
    }
}
