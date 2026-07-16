using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Sources;

public sealed class SummarizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<SummarizationService> _logger;

    public SummarizationService(HttpClient httpClient, ILogger<SummarizationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> SummarizeAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/summarize", new
            {
                text = text,
                maxLength = 500
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Transcriber summarization returned status {Status}: {Error}", response.StatusCode, errContent);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<SummarizeResponse>(JsonOptions, ct);
            if (payload != null && string.Equals(payload.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return payload.Summary;
            }

            _logger.LogWarning("Transcriber summarization failed: {Error}", payload?.Error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call summarization service.");
            return null;
        }
    }

    private sealed record SummarizeResponse(string Status, string? Summary, string? Error);
}
