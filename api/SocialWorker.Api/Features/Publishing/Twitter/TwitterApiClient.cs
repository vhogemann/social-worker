using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.Twitter;

public class TwitterApiClient
{
    private readonly HttpClient _http;
    private readonly TwitterOptions _options;
    private readonly ILogger<TwitterApiClient> _logger;

    public TwitterApiClient(
        HttpClient http,
        IOptions<TwitterOptions> options,
        ILogger<TwitterApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TwitterTweetData> CreateTweetAsync(
        string accessToken,
        string text,
        string? inReplyToTweetId = null,
        CancellationToken ct = default)
    {
        var url = CombineUrl(_options.BaseUrl, "tweets");
        var payload = new TwitterCreateTweetRequest(
            Text: text,
            Reply: !string.IsNullOrWhiteSpace(inReplyToTweetId) ? new TwitterReplyOptions(inReplyToTweetId) : null);

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Twitter create tweet failed ({StatusCode}): {Body}", response.StatusCode, body);
            var errorMsg = ExtractErrorMessage(body);
            throw new InvalidOperationException($"Twitter API returned {(int)response.StatusCode}: {errorMsg}");
        }

        var result = JsonSerializer.Deserialize<TwitterCreateTweetResponse>(body);
        if (result?.Data == null || string.IsNullOrWhiteSpace(result.Data.Id))
        {
            throw new InvalidOperationException("Twitter returned an empty tweet response.");
        }

        return result.Data;
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
        return baseUrl.TrimEnd('/') + "/" + relativePath.TrimStart('/');
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail)) return detail.GetString() ?? body;
            if (root.TryGetProperty("title", out var title)) return title.GetString() ?? body;
            if (root.TryGetProperty("error", out var error)) return error.GetString() ?? body;
            if (root.TryGetProperty("error_description", out var desc)) return desc.GetString() ?? body;
        }
        catch
        {
        }
        return body;
    }
}
