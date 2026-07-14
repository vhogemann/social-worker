using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Sources;

public sealed record TranscriptExtractionResult(bool Success, string? TranscriptPath, double? Duration, string? Language, string? Error);

public sealed record TranscriptDocument(string? VideoUrl, string? Language, double? Duration, string? Transcript);

public interface ITranscriptExtractionService
{
    Task<bool> HealthAsync(CancellationToken ct);
    Task<TranscriptExtractionResult> ExtractAsync(string videoUrl, string outputPath, CancellationToken ct);
    Task<TranscriptDocument?> ReadTranscriptAsync(string transcriptPath, CancellationToken ct);
}

public sealed class TranscriptExtractionService : ITranscriptExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TranscriberOptions _options;

    public TranscriptExtractionService(HttpClient httpClient, IOptions<TranscriberOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> HealthAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync("/health", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<TranscriptExtractionResult> ExtractAsync(string videoUrl, string outputPath, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/extract-transcript", new
        {
            videoUrl,
            outputPath,
        }, ct);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TranscriptExtractionResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Transcriber returned an empty response.");

        return new TranscriptExtractionResult(
            string.Equals(payload.Status, "success", StringComparison.OrdinalIgnoreCase),
            payload.TranscriptPath,
            payload.Duration,
            payload.Language,
            payload.Error);
    }

    public async Task<TranscriptDocument?> ReadTranscriptAsync(string transcriptPath, CancellationToken ct)
    {
        var fullPath = Path.Combine(_options.TranscriptsPath, transcriptPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(fullPath, ct);
        return JsonSerializer.Deserialize<TranscriptDocument>(json, JsonOptions);
    }

    private sealed record TranscriptExtractionResponse(string Status, string? TranscriptPath, double? Duration, string? Language, string? Error);
}