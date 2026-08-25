using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.ImageEngine;

public sealed class ImageEngineClient : IImageEngineClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageEngineClient> _logger;

    public ImageEngineClient(HttpClient httpClient, ILogger<ImageEngineClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ImageTransformResult> ResizeImageAsync(
        Stream stream,
        string mimeType,
        int maxDimension = 1200,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType);
        content.Add(streamContent, "file", "image.bin");
        content.Add(new StringContent(maxDimension.ToString()), "max_dimension");

        var response = await _httpClient.PostAsync("/transform/resize", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("ImageEngine resize failed with status {Status}: {Error}", response.StatusCode, err);
            throw new InvalidOperationException($"Image resize failed: {response.StatusCode} - {err}");
        }

        var data = await response.Content.ReadAsByteArrayAsync(ct);
        var resMime = response.Content.Headers.ContentType?.MediaType ?? mimeType;

        var width = TryGetHeaderInt(response, "X-Width", maxDimension);
        var height = TryGetHeaderInt(response, "X-Height", maxDimension);

        return new ImageTransformResult(data, width, height, resMime);
    }

    public async Task<ImageTransformResult> FormatAspectRatioAsync(
        Stream stream,
        string mimeType,
        string targetRatio = "1:1",
        string fitMode = "blur_padding",
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType);
        content.Add(streamContent, "file", "image.bin");
        content.Add(new StringContent(targetRatio), "target_ratio");
        content.Add(new StringContent(fitMode), "fit_mode");

        var response = await _httpClient.PostAsync("/transform/aspect-ratio", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("ImageEngine aspect-ratio transform failed with status {Status}: {Error}", response.StatusCode, err);
            throw new InvalidOperationException($"Aspect-ratio transform failed: {response.StatusCode} - {err}");
        }

        var data = await response.Content.ReadAsByteArrayAsync(ct);
        var resMime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

        var width = TryGetHeaderInt(response, "X-Width", 1080);
        var height = TryGetHeaderInt(response, "X-Height", 1080);

        return new ImageTransformResult(data, width, height, resMime);
    }

    public async Task<byte[]> RenderTemplateAsync(
        string templateJson,
        IDictionary<string, string>? slots = null,
        IDictionary<string, (Stream Stream, string FileName, string MimeType)>? images = null,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        content.Add(new StringContent(templateJson), "template");

        if (slots != null && slots.Count > 0)
        {
            var slotsJson = JsonSerializer.Serialize(slots);
            content.Add(new StringContent(slotsJson), "slots");
        }

        if (images != null)
        {
            foreach (var kvp in images)
            {
                var (imgStream, fileName, mime) = kvp.Value;
                var sc = new StreamContent(imgStream);
                sc.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(mime) ? "image/png" : mime);
                content.Add(sc, kvp.Key, string.IsNullOrWhiteSpace(fileName) ? $"{kvp.Key}.png" : fileName);
            }
        }

        var response = await _httpClient.PostAsync("/render/template", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("ImageEngine template render failed with status {Status}: {Error}", response.StatusCode, err);
            throw new InvalidOperationException($"Template render failed: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImageEngine health check failed");
            return false;
        }
    }

    private static int TryGetHeaderInt(HttpResponseMessage response, string headerName, int fallback)
    {
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            var str = values.FirstOrDefault();
            if (int.TryParse(str, out var parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }
}
