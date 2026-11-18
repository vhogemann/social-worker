using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.Mastodon;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Mastodon;

public sealed class MastodonOAuthServiceTests
{
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> RequestBodies { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            RequestBodies.Add(body);
            return Handler(request, body);
        }
    }

    [Theory]
    [InlineData("mastodon.social", "https://mastodon.social")]
    [InlineData("HTTPS://FOSSTODON.ORG/", "https://fosstodon.org")]
    [InlineData("@alice@hachyderm.io", "https://hachyderm.io")]
    [InlineData("https://social.example.com:8443/", "https://social.example.com:8443")]
    public void NormalizeInstanceUrl_ValidInputs_ReturnsNormalizedHttpsUrl(string input, string expected)
    {
        var result = MastodonOAuthService.NormalizeInstanceUrl(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://mastodon.social")]
    [InlineData("localhost")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://192.168.1.10")]
    [InlineData("https://10.0.0.5")]
    [InlineData("https://api.internal")]
    public void NormalizeInstanceUrl_InvalidOrBlockedInputs_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => MastodonOAuthService.NormalizeInstanceUrl(input));
    }

    [Fact]
    public void FormatHandle_FormatsWithInstanceHost()
    {
        Assert.Equal("@alice@mastodon.social", MastodonOAuthService.FormatHandle("alice", "https://mastodon.social"));
        Assert.Equal("@alice@mastodon.social", MastodonOAuthService.FormatHandle("@alice", "https://mastodon.social"));
        Assert.Equal("@alice@fosstodon.org", MastodonOAuthService.FormatHandle("alice@fosstodon.org", "https://mastodon.social"));
    }

    [Fact]
    public void TryExtractInstanceFromHandle_ParsesDomain()
    {
        Assert.Equal("mastodon.social", MastodonOAuthService.TryExtractInstanceFromHandle("@alice@mastodon.social"));
        Assert.Equal("fosstodon.org", MastodonOAuthService.TryExtractInstanceFromHandle("bob@fosstodon.org"));
        Assert.Null(MastodonOAuthService.TryExtractInstanceFromHandle("@alice"));
    }

    [Fact]
    public async Task RegisterAppExchangeAndVerify_ExecutesOAuthFlow()
    {
        var handler = new StubHttpHandler
        {
            Handler = (req, _) =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (path == "/api/v1/apps")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"id":"1","client_id":"cid-123","client_secret":"csec-456"}""", Encoding.UTF8, "application/json")
                    };
                }

                if (path == "/oauth/token")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"access_token":"tok-789","token_type":"Bearer","scope":"read write"}""", Encoding.UTF8, "application/json")
                    };
                }

                if (path == "/api/v1/accounts/verify_credentials")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"id":"42","username":"alice","acct":"alice"}""", Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var service = new MastodonOAuthService(new HttpClient(handler), Options.Create(new MastodonOptions()));

        var app = await service.RegisterAppAsync("https://mastodon.social", "https://app.local/oauth/callback/mastodon");
        Assert.Equal("cid-123", app.ClientId);
        Assert.Equal("csec-456", app.ClientSecret);

        var authUrl = service.GetAuthorizationUrl("https://mastodon.social", app.ClientId, "https://app.local/oauth/callback/mastodon", "nonce-1");
        Assert.Contains("https://mastodon.social/oauth/authorize?", authUrl);
        Assert.Contains("client_id=cid-123", authUrl);
        Assert.Contains("state=nonce-1", authUrl);

        var token = await service.ExchangeCodeAsync("https://mastodon.social", app.ClientId, app.ClientSecret, "https://app.local/oauth/callback/mastodon", "code-abc");
        Assert.Equal("tok-789", token.AccessToken);

        var profile = await service.VerifyCredentialsAsync("https://mastodon.social", token.AccessToken);
        Assert.Equal("42", profile.Id);
        Assert.Equal("alice", profile.Username);
    }
}
