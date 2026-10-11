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

public sealed class InstagramBusinessDiscoveryClientTests
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
    public async Task GetProfileWithMediaAsync_ParsesGraphApiResponse()
    {
        var responseJson = """
        {
          "business_discovery": {
            "id": "1784140001",
            "username": "flaviobolsonaro",
            "name": "Flávio Bolsonaro",
            "profile_picture_url": "https://example.com/pic.jpg",
            "biography": "Senador",
            "media": {
              "data": [
                {
                  "id": "1804291823",
                  "caption": "Post do senado",
                  "media_type": "IMAGE",
                  "media_url": "https://example.com/media.jpg",
                  "permalink": "https://www.instagram.com/p/DB123",
                  "timestamp": "2026-10-09T10:00:00+0000"
                }
              ]
            }
          },
          "id": "1784149999"
        }
        """;

        var handler = new TestHttpMessageHandler(req =>
        {
            var query = Uri.UnescapeDataString(req.RequestUri?.Query ?? "");
            Assert.Contains("business_discovery.username(flaviobolsonaro)", query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        });

        var http = new HttpClient(handler);
        var options = Options.Create(new InstagramOptions());
        var client = new InstagramBusinessDiscoveryClient(http, options, NullLogger<InstagramBusinessDiscoveryClient>.Instance);

        var result = await client.GetProfileWithMediaAsync("1784149999", "flaviobolsonaro", "fake_token", 5);

        Assert.NotNull(result);
        Assert.Equal("flaviobolsonaro", result.Username);
        Assert.Equal("Flávio Bolsonaro", result.Name);
        Assert.NotNull(result.Media?.Data);
        Assert.Single(result.Media.Data);
        Assert.Equal("Post do senado", result.Media.Data[0].Caption);
    }

    [Fact]
    public async Task GetProfileWithMediaAsync_WhenApiReturnsError_ThrowsDescriptiveException()
    {
        var errorJson = """
        {
          "error": {
            "message": "(#100) Tried accessing nonexisting field (business_discovery)",
            "type": "OAuthException",
            "code": 100,
            "error_subcode": 2108006
          }
        }
        """;

        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorJson)
        });

        var http = new HttpClient(handler);
        var options = Options.Create(new InstagramOptions());
        var client = new InstagramBusinessDiscoveryClient(http, options, NullLogger<InstagramBusinessDiscoveryClient>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetProfileWithMediaAsync("1784149999", "target_user", "token"));

        Assert.Contains("Tried accessing nonexisting field", ex.Message);
    }

    [Fact]
    public async Task ResolveCallerIgUserIdAsync_ViaMeEndpoint_ReturnsId()
    {
        var meJson = """
        {
          "id": "12345",
          "instagram_business_account": {
            "id": "1784140099",
            "username": "mycreator",
            "name": "My Creator Profile"
          }
        }
        """;

        var handler = new TestHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(meJson)
            };
        });

        var http = new HttpClient(handler);
        var options = Options.Create(new InstagramOptions());
        var client = new InstagramBusinessDiscoveryClient(http, options, NullLogger<InstagramBusinessDiscoveryClient>.Instance);

        var id = await client.ResolveCallerIgUserIdAsync("token");

        Assert.Equal("1784140099", id);
    }
}
