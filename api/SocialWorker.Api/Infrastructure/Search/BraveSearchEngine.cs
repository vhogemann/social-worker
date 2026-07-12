using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Infrastructure.Search;

public sealed class BraveSearchEngine : ISearchEngine
{
    private readonly HttpClient _client;
    private readonly SearchOptions _options;

    public BraveSearchEngine(HttpClient client, IOptions<SearchOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        return await SearchAsyncInternal("web", query, ct);
    }

    public async Task<List<SearchResult>> SearchImagesAsync(string query, CancellationToken ct)
    {
        return await SearchAsyncInternal("images", query, ct);
    }

    private async Task<List<SearchResult>> SearchAsyncInternal(string resource, string query, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        if (string.IsNullOrWhiteSpace(_options.BraveApiKey))
        {
            throw new InvalidOperationException("Brave Search API Key is not configured.");
        }

        var requestUri = $"https://api.search.brave.com/res/v1/{resource}/search?q={Uri.EscapeDataString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("X-Subscription-Token", _options.BraveApiKey);

        using var response = await _client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonString);

        if (doc.RootElement.TryGetProperty(resource, out var resourceProp) &&
            resourceProp.TryGetProperty("results", out var resultsProp) &&
            resultsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsProp.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var url = "";
                if (resource == "images")
                {
                    if (item.TryGetProperty("thumbnail", out var thumb) && thumb.TryGetProperty("src", out var src))
                        url = src.GetString() ?? "";
                    else if (item.TryGetProperty("url", out var imgUrl))
                        url = imgUrl.GetString() ?? "";
                }
                else
                {
                    if (item.TryGetProperty("url", out var webUrl))
                        url = webUrl.GetString() ?? "";
                }
                var snippet = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                results.Add(new SearchResult(title, url, snippet));
            }
        }

        return results;
    }
}
