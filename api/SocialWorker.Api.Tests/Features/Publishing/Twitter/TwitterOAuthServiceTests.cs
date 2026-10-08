using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.Twitter;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Twitter;

public sealed class TwitterOAuthServiceTests
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

    private static (TwitterOAuthService Service, TwitterOptions Options) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string clientSecret = "test_secret_456")
    {
        var options = new TwitterOptions
        {
            ClientId = "test_client_123",
            ClientSecret = clientSecret,
            RedirectUri = "https://app.social-worker.local/oauth/callback/twitter",
            Scopes = "tweet.read tweet.write users.read offline.access"
        };
        var http = new HttpClient(new DelegatingHandlerStub(handler));
        var service = new TwitterOAuthService(http, Options.Create(options), NullLogger<TwitterOAuthService>.Instance);
        return (service, options);
    }

    [Fact]
    public void GetAuthorizationUrl_BuildsValidUrlWithPkce()
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var url = service.GetAuthorizationUrl("test_nonce_789", "challenge_abc_123");

        Assert.Contains("client_id=test_client_123", url);
        Assert.Contains("state=test_nonce_789", url);
        Assert.Contains("code_challenge=challenge_abc_123", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("response_type=code", url);
        Assert.StartsWith("https://twitter.com/i/oauth2/authorize", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ConfidentialClient_SendsBasicAuth()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var (service, opts) = CreateService(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            var resp = new TwitterTokenResponse(
                TokenType: "bearer",
                ExpiresIn: 7200,
                AccessToken: "access_token_xyz",
                Scope: "tweet.read tweet.write",
                RefreshToken: "refresh_token_xyz");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        }, clientSecret: "secret789");

        var result = await service.ExchangeCodeAsync("auth_code_123", "verifier_456");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Basic", capturedRequest.Headers.Authorization?.Scheme);
        var expectedBasic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opts.ClientId}:secret789"));
        Assert.Equal(expectedBasic, capturedRequest.Headers.Authorization?.Parameter);

        Assert.Contains("code=auth_code_123", capturedBody);
        Assert.Contains("code_verifier=verifier_456", capturedBody);
        Assert.Contains("grant_type=authorization_code", capturedBody);

        Assert.Equal("access_token_xyz", result.AccessToken);
        Assert.Equal("refresh_token_xyz", result.RefreshToken);
        Assert.Equal(7200, result.ExpiresIn);
    }

    [Fact]
    public async Task ExchangeCodeAsync_PublicClient_DoesNotSendBasicAuth()
    {
        HttpRequestMessage? capturedRequest = null;

        var (service, opts) = CreateService(req =>
        {
            capturedRequest = req;
            var resp = new TwitterTokenResponse(
                TokenType: "bearer",
                ExpiresIn: 7200,
                AccessToken: "access_token_pub",
                Scope: "tweet.read tweet.write",
                RefreshToken: "refresh_token_pub");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        }, clientSecret: "");

        var result = await service.ExchangeCodeAsync("auth_code_pub", "verifier_pub");

        Assert.NotNull(capturedRequest);
        Assert.Null(capturedRequest.Headers.Authorization);
        Assert.Equal("access_token_pub", result.AccessToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_RotatesTokens()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var (service, _) = CreateService(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            var resp = new TwitterTokenResponse(
                TokenType: "bearer",
                ExpiresIn: 7200,
                AccessToken: "new_access_token",
                Scope: "tweet.read tweet.write",
                RefreshToken: "new_rotated_refresh_token");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        });

        var result = await service.RefreshTokenAsync("old_refresh_token");

        Assert.NotNull(capturedRequest);
        Assert.Contains("grant_type=refresh_token", capturedBody);
        Assert.Contains("refresh_token=old_refresh_token", capturedBody);
        Assert.Equal("new_access_token", result.AccessToken);
        Assert.Equal("new_rotated_refresh_token", result.RefreshToken);
    }

    [Fact]
    public async Task FetchUserProfileAsync_ReturnsUserData()
    {
        HttpRequestMessage? capturedRequest = null;

        var (service, _) = CreateService(req =>
        {
            capturedRequest = req;
            var resp = new TwitterUserResponse(new TwitterUserData("123456789", "Alice Test", "alicetest"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), Encoding.UTF8, "application/json")
            };
        });

        var profile = await service.FetchUserProfileAsync("my_access_token");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("my_access_token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Equal("123456789", profile.Id);
        Assert.Equal("alicetest", profile.Username);
    }
}
