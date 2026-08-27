using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ImageEngine;

public sealed class ImageEngineClientTests
{
    [Fact]
    public async Task ResizeImageAsync_ReturnsValidResult_WhenSuccessful()
    {
        var expectedBytes = new byte[] { 10, 20, 30 };
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/transform/resize", req.RequestUri!.AbsolutePath);

            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            res.Headers.Add("X-Width", "800");
            res.Headers.Add("X-Height", "600");
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8103") };
        var engineClient = new ImageEngineClient(client, NullLogger<ImageEngineClient>.Instance);

        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await engineClient.ResizeImageAsync(stream, "image/jpeg", 800, CancellationToken.None);

        Assert.Equal(expectedBytes, result.Data);
        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
        Assert.Equal("image/jpeg", result.MimeType);
    }

    [Fact]
    public async Task FormatAspectRatioAsync_ReturnsValidResult()
    {
        var expectedBytes = new byte[] { 5, 6, 7 };
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/transform/aspect-ratio", req.RequestUri!.AbsolutePath);

            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            res.Headers.Add("X-Width", "1080");
            res.Headers.Add("X-Height", "1080");
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8103") };
        var engineClient = new ImageEngineClient(client, NullLogger<ImageEngineClient>.Instance);

        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await engineClient.FormatAspectRatioAsync(stream, "image/jpeg", "1:1", "blur_padding", CancellationToken.None);

        Assert.Equal(expectedBytes, result.Data);
        Assert.Equal(1080, result.Width);
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public async Task RenderTemplateAsync_ReturnsRenderedPngBytes()
    {
        var expectedBytes = new byte[] { 100, 101, 102 };
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/render/template", req.RequestUri!.AbsolutePath);

            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8103") };
        var engineClient = new ImageEngineClient(client, NullLogger<ImageEngineClient>.Instance);

        var templateJson = "{\"name\":\"quote\",\"width\":1080,\"height\":1080,\"layers\":[]}";
        var slots = new Dictionary<string, string> { { "quote", "Hello" } };

        var result = await engineClient.RenderTemplateAsync(templateJson, slots, null, CancellationToken.None);

        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithImages_SendsMultipartCorrectly()
    {
        var expectedBytes = new byte[] { 200, 201 };
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/render/template", req.RequestUri!.AbsolutePath);
            Assert.NotNull(req.Content);

            var readBytes = req.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            Assert.NotEmpty(readBytes);

            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8103") };
        var engineClient = new ImageEngineClient(client, NullLogger<ImageEngineClient>.Instance);

        var sharedStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var images = new Dictionary<string, (Stream Stream, string FileName, string MimeType)>
        {
            { "slot1", (sharedStream, "img1.png", "image/png") },
            { "slot2", (sharedStream, "img2.png", "image/png") }
        };

        var result = await engineClient.RenderTemplateAsync("{}", null, images, CancellationToken.None);
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task HealthCheckAsync_ReturnsTrue_On200()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("/health", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8103") };
        var engineClient = new ImageEngineClient(client, NullLogger<ImageEngineClient>.Instance);

        var healthy = await engineClient.HealthCheckAsync(CancellationToken.None);
        Assert.True(healthy);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
