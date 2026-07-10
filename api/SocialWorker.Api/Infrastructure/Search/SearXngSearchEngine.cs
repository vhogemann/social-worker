using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Infrastructure.Search;

public sealed class SearXngSearchEngine : ISearchEngine
{
    private readonly HttpClient _client;
    private readonly SearchOptions _options;

    public SearXngSearchEngine(HttpClient client, IOptions<SearchOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        var baseUrl = _options.SearXngBaseUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/search?q={Uri.EscapeDataString(query)}&format=json";

        using var response = await _client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonString);

        if (doc.RootElement.TryGetProperty("results", out var resultsProp) &&
            resultsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsProp.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var snippet = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                results.Add(new SearchResult(title, url, snippet));
            }
        }

        return results;
    }

    public async Task<List<SearchResult>> SearchImagesAsync(string query, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        var baseUrl = _options.SearXngBaseUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/search?q={Uri.EscapeDataString(query)}&format=json&categories=images";

        using var response = await _client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonString);

        if (doc.RootElement.TryGetProperty("results", out var resultsProp) &&
            resultsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsProp.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var imgUrl = item.TryGetProperty("img_src", out var img) ? img.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(imgUrl))
                {
                    imgUrl = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                }
                var snippet = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                
                if (!string.IsNullOrEmpty(imgUrl))
                {
                    results.Add(new SearchResult(title, imgUrl, snippet));
                }
            }
        }

        return results;
    }
}
