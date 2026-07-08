using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Infrastructure.Llm;

public class ModelCapabilityProbe
{
    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;

    public ModelCapabilityProbe(HttpClient client, IMemoryCache cache)
    {
        _client = client;
        _cache = cache;
    }

    public async Task<ModelCapabilities> GetCapabilitiesAsync(LlmProvider provider)
    {
        var cacheKey = $"caps:{provider.ProviderType}:{provider.Model}";
        if (_cache.TryGetValue<ModelCapabilities>(cacheKey, out var cached))
        {
            return cached!;
        }

        var caps = await ProbeCapabilitiesAsync(provider);
        _cache.Set(cacheKey, caps, TimeSpan.FromHours(1));
        return caps;
    }

    private async Task<ModelCapabilities> ProbeCapabilitiesAsync(LlmProvider provider)
    {
        var type = provider.ProviderType;
        var model = provider.Model;

        if (string.Equals(type, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var supportsVision = model.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase) ||
                                 model.Contains("gpt-4-vision", StringComparison.OrdinalIgnoreCase) ||
                                 model.Contains("gpt-5", StringComparison.OrdinalIgnoreCase) ||
                                 model.Contains("-vision", StringComparison.OrdinalIgnoreCase) ||
                                 model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
                                 model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
                                 model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);

            return new ModelCapabilities(SupportsVision: supportsVision, SupportsTools: true);
        }

        if (string.Equals(type, "OpenRouter", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var requestUrl = $"{provider.BaseUrl.TrimEnd('/')}/models";
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                if (!string.IsNullOrEmpty(provider.ApiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", provider.ApiKey);
                }

                var response = await _client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>();
                    if (result?.Data != null)
                    {
                        var found = Array.Find(result.Data, m => string.Equals(m.Id, model, StringComparison.OrdinalIgnoreCase));
                        if (found?.Architecture != null)
                        {
                            var inputModalities = found.Architecture.InputModalities;
                            var supportsVision = inputModalities != null && Array.Exists(inputModalities, val => string.Equals(val, "image", StringComparison.OrdinalIgnoreCase));
                            return new ModelCapabilities(SupportsVision: supportsVision, SupportsTools: true);
                        }
                    }
                }
            }
            catch
            {
            }

            return new ModelCapabilities(SupportsVision: model.Contains("vision", StringComparison.OrdinalIgnoreCase) || model.Contains("claude-3", StringComparison.OrdinalIgnoreCase) || model.Contains("gpt-4", StringComparison.OrdinalIgnoreCase), SupportsTools: true);
        }

        if (string.Equals(type, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var baseUri = new Uri(provider.BaseUrl);
                var apiShowUrl = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}/api/show";

                var response = await _client.PostAsJsonAsync(apiShowUrl, new { name = model });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OllamaShowResponse>();
                    if (result?.Capabilities != null)
                    {
                        var supportsVision = Array.Exists(result.Capabilities, val => string.Equals(val, "vision", StringComparison.OrdinalIgnoreCase));
                        var supportsTools = Array.Exists(result.Capabilities, val => string.Equals(val, "tools", StringComparison.OrdinalIgnoreCase));
                        return new ModelCapabilities(SupportsVision: supportsVision, SupportsTools: supportsTools);
                    }
                    if (result?.Details?.Families != null)
                    {
                        var supportsVision = Array.Exists(result.Details.Families, val => string.Equals(val, "clip", StringComparison.OrdinalIgnoreCase));
                        return new ModelCapabilities(SupportsVision: supportsVision, SupportsTools: true);
                    }
                }
            }
            catch
            {
            }

            return new ModelCapabilities(SupportsVision: model.Contains("vision", StringComparison.OrdinalIgnoreCase), SupportsTools: true);
        }

        return new ModelCapabilities(SupportsVision: false, SupportsTools: false);
    }

    private class OpenRouterModelsResponse
    {
        [JsonPropertyName("data")]
        public OpenRouterModelInfo[]? Data { get; set; }
    }

    private class OpenRouterModelInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("architecture")]
        public OpenRouterArchitecture? Architecture { get; set; }
    }

    private class OpenRouterArchitecture
    {
        [JsonPropertyName("input_modalities")]
        public string[]? InputModalities { get; set; }
    }

    private class OllamaShowResponse
    {
        [JsonPropertyName("capabilities")]
        public string[]? Capabilities { get; set; }

        [JsonPropertyName("details")]
        public OllamaDetails? Details { get; set; }
    }

    private class OllamaDetails
    {
        [JsonPropertyName("families")]
        public string[]? Families { get; set; }
    }
}
