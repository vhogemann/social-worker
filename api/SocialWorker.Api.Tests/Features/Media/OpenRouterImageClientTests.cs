using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Features.Media.Models;
using SocialWorker.Api.Features.Media.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Media;

public sealed class OpenRouterImageClientTests
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

    [Fact]
    public async Task GenerateImageAsync_Throws_WhenPromptIsEmpty()
    {
        var http = new HttpClient(new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://openrouter.ai")
        };
        var client = new OpenRouterImageClient(http, NullLogger<OpenRouterImageClient>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GenerateImageAsync("   "));
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsBytes_WhenB64JsonReturned()
    {
        var expectedBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var b64String = Convert.ToBase64String(expectedBytes);

        var handler = new DelegatingHandlerStub(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/images", req.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("sk-test-key", req.Headers.Authorization?.Parameter);

            var body = req.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            Assert.Equal("Generate a cyberpunk cat", doc.RootElement.GetProperty("prompt").GetString());
            Assert.Equal("black-forest-labs/flux-3-image-klein", doc.RootElement.GetProperty("model").GetString());
            Assert.Equal("16:9", doc.RootElement.GetProperty("aspect_ratio").GetString());

            var jsonResp = JsonSerializer.Serialize(new OpenRouterImageResponse(new[]
            {
                new OpenRouterImageData(b64String, null)
            }));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResp, Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai") };
        var client = new OpenRouterImageClient(http, NullLogger<OpenRouterImageClient>.Instance);

        var result = await client.GenerateImageAsync("Generate a cyberpunk cat", null, "16:9", "sk-test-key");

        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task GenerateImageAsync_Remaps_Legacy_Flux1Schnell_To_Flux3ImageKlein()
    {
        var expectedBytes = new byte[] { 0x89, 0x50 };
        var b64String = Convert.ToBase64String(expectedBytes);

        var handler = new DelegatingHandlerStub(req =>
        {
            var body = req.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            Assert.Equal("black-forest-labs/flux-3-image-klein", doc.RootElement.GetProperty("model").GetString());

            var jsonResp = JsonSerializer.Serialize(new OpenRouterImageResponse(new[]
            {
                new OpenRouterImageData(b64String, null)
            }));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResp, Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai") };
        var client = new OpenRouterImageClient(http, NullLogger<OpenRouterImageClient>.Instance);

        var result = await client.GenerateImageAsync("Prompt", "black-forest-labs/flux-1-schnell", "1:1", "sk-test");
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task GenerateImageAsync_DownloadsUrl_WhenUrlReturned()
    {
        var expectedBytes = new byte[] { 1, 2, 3, 4, 5 };

        var handler = new DelegatingHandlerStub(req =>
        {
            if (req.Method == HttpMethod.Post)
            {
                var jsonResp = JsonSerializer.Serialize(new OpenRouterImageResponse(new[]
                {
                    new OpenRouterImageData(null, "https://openrouter.ai/sample-image.png")
                }));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResp, Encoding.UTF8, "application/json")
                };
            }

            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == "/sample-image.png")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(expectedBytes)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai") };
        var client = new OpenRouterImageClient(http, NullLogger<OpenRouterImageClient>.Instance);

        var result = await client.GenerateImageAsync("Prompt", "custom-model", "1:1", "sk-key");

        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task GenerateImageAsync_Throws_WhenApiReturnsError()
    {
        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Invalid model\"}", Encoding.UTF8, "application/json")
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai") };
        var client = new OpenRouterImageClient(http, NullLogger<OpenRouterImageClient>.Instance);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GenerateImageAsync("Prompt", apiKey: "key"));

        Assert.Contains("Invalid model", ex.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_Throws_WhenResponseHasNoData()
    {
        var handler = new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai") };
        var client = new OpenRouterImageClient(http, NullLogger<OpenRouterImageClient>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GenerateImageAsync("Prompt", apiKey: "key"));
    }
}
