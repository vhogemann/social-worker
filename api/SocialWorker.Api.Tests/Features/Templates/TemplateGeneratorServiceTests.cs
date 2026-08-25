using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Templates.Services;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Templates;

public sealed class TemplateGeneratorServiceTests
{
    private sealed class MockLlmAdapter : ILlmProviderAdapter
    {
        private readonly Func<OpenAiModels.ChatCompletionRequest, OpenAiModels.ChatCompletionResponse> _handler;

        public MockLlmAdapter(Func<OpenAiModels.ChatCompletionRequest, OpenAiModels.ChatCompletionResponse> handler)
        {
            _handler = handler;
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct = default)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(_handler(request));
        }

        public IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task GenerateTemplateAsync_ParsesJsonAndRendersPreview()
    {
        var validJson = """
        {
          "name": "Neon Quote",
          "description": "Cyberpunk style quote card",
          "width": 1080,
          "height": 1080,
          "slots": [
            { "name": "quote", "type": "text", "required": true }
          ],
          "layers": [
            { "type": "rect", "fill": "#000000", "x": 0, "y": 0, "w": "100%", "h": "100%" },
            { "type": "text", "slot": "quote", "x": 100, "y": 100, "fontSize": 48, "color": "#00ffff" }
          ]
        }
        """;

        var adapter = new MockLlmAdapter(req => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new()
            {
                new()
                {
                    Message = new() { Role = "assistant", Content = $"```json\n{validJson}\n```" }
                }
            }
        });

        var fakeEngine = new FakeImageEngineClient();
        var service = new TemplateGeneratorService(adapter, fakeEngine, NullLogger<TemplateGeneratorService>.Instance);
        var credentials = new LlmCredentials("https://api.test", "key", "gpt-4");

        var result = await service.GenerateTemplateAsync("create neon quote", credentials, 1080, 1080);

        Assert.NotNull(result);
        Assert.Equal("Neon Quote", result.Name);
        Assert.Equal("Cyberpunk style quote card", result.Description);
        Assert.Equal(1080, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.NotNull(result.PreviewBase64);
    }

    private sealed class FailingThenSucceedingEngine : IImageEngineClient
    {
        private int _attempts = 0;

        public Task<byte[]> RenderTemplateAsync(
            string templateJson,
            IDictionary<string, string>? slots = null,
            IDictionary<string, (Stream Stream, string FileName, string MimeType)>? images = null,
            CancellationToken ct = default)
        {
            _attempts++;
            if (_attempts == 1)
            {
                throw new InvalidOperationException("Invalid font family");
            }

            return Task.FromResult(new byte[] { 1, 2, 3, 4 });
        }

        public Task<ImageTransformResult> ResizeImageAsync(Stream stream, string mimeType, int maxDimension = 1200, CancellationToken ct = default)
            => Task.FromResult(new ImageTransformResult(new byte[] { 1 }, 100, 100, mimeType));

        public Task<ImageTransformResult> FormatAspectRatioAsync(Stream stream, string mimeType, string targetRatio = "1:1", string fitMode = "blur_padding", CancellationToken ct = default)
            => Task.FromResult(new ImageTransformResult(new byte[] { 1 }, 1080, 1080, mimeType));

        public Task<bool> HealthCheckAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task GenerateTemplateAsync_PerformsOneShotAutoRepairOnRenderFailure()
    {
        var callCount = 0;
        var validJson = "{\"name\":\"Repaired Template\",\"width\":1080,\"height\":1080,\"slots\":[],\"layers\":[]}";

        var adapter = new MockLlmAdapter(req =>
        {
            callCount++;
            return new OpenAiModels.ChatCompletionResponse
            {
                Choices = new()
                {
                    new()
                    {
                        Message = new() { Role = "assistant", Content = validJson }
                    }
                }
            };
        });

        var engine = new FailingThenSucceedingEngine();
        var service = new TemplateGeneratorService(adapter, engine, NullLogger<TemplateGeneratorService>.Instance);
        var credentials = new LlmCredentials("https://api.test", "key", "gpt-4");

        var result = await service.GenerateTemplateAsync("create card", credentials);

        Assert.Equal(2, callCount); // Initial call + 1-shot repair call
        Assert.NotNull(result);
        Assert.Equal("Repaired Template", result.Name);
        Assert.NotNull(result.PreviewBase64);
    }
}
