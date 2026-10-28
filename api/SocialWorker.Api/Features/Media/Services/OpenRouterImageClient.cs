using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Media.Models;

namespace SocialWorker.Api.Features.Media.Services;

public interface IImageGenerationClient
{
    Task<byte[]> GenerateImageAsync(
        string prompt,
        string? model = null,
        string? aspectRatio = "1:1",
        string? apiKey = null,
        CancellationToken ct = default);
}

public sealed class OpenRouterImageClient : IImageGenerationClient
{
    public const string DefaultModel = "black-forest-labs/flux-3-image-klein";
    private readonly HttpClient _http;
    private readonly ILogger<OpenRouterImageClient> _logger;

    public OpenRouterImageClient(HttpClient http, ILogger<OpenRouterImageClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<byte[]> GenerateImageAsync(
        string prompt,
        string? model = null,
        string? aspectRatio = "1:1",
        string? apiKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        var selectedModel = !string.IsNullOrWhiteSpace(model) ? model.Trim() : DefaultModel;
        if (selectedModel.Equals("black-forest-labs/flux-1-schnell", StringComparison.OrdinalIgnoreCase) ||
            selectedModel.Equals("black-forest-labs/flux-schnell", StringComparison.OrdinalIgnoreCase))
        {
            selectedModel = DefaultModel;
        }

        var requestPayload = new OpenRouterImageRequest(prompt, selectedModel, aspectRatio, 1);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/images");
        request.Content = JsonContent.Create(requestPayload);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        _logger.LogInformation("Generating image with OpenRouter model {Model}, aspect {AspectRatio}...", selectedModel, aspectRatio);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenRouter image generation failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            throw new HttpRequestException($"OpenRouter image generation failed ({response.StatusCode}): {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenRouterImageResponse>(cancellationToken: ct);
        if (result?.Data == null || result.Data.Length == 0)
        {
            throw new InvalidOperationException("OpenRouter returned empty data in image generation response.");
        }

        var item = result.Data[0];
        if (!string.IsNullOrWhiteSpace(item.B64Json))
        {
            return Convert.FromBase64String(item.B64Json);
        }

        if (!string.IsNullOrWhiteSpace(item.Url) && Uri.TryCreate(item.Url, UriKind.Absolute, out var imageUri))
        {
            return await _http.GetByteArrayAsync(imageUri, ct);
        }

        throw new InvalidOperationException("OpenRouter image response contained neither b64_json nor valid url.");
    }
}
