using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Chat;

public sealed class OpenAiProviderAdapter : ILlmProviderAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiProviderAdapter> _log;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAiProviderAdapter(HttpClient http, ILogger<OpenAiProviderAdapter> log)
    {
        _http = http;
        _log = log;
    }

    public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, [EnumeratorCancellation] CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{credentials.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(credentials.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
        }
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _log.LogError("OpenAI API error {Status}: {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"Upstream {resp.StatusCode}: {err}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;
            var data = line.Substring("data:".Length).Trim();
            if (data == "[DONE]") yield break;

            OpenAiModels.StreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAiModels.StreamChunk>(data);
            }
            catch (JsonException ex)
            {
                _log.LogWarning("Failed to parse chunk: {Data} ({Msg})", data, ex.Message);
                continue;
            }

            if (chunk is not null) yield return chunk;
        }
    }

    public async Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{credentials.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(credentials.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
        }

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _log.LogWarning("OpenAI non-stream API error: {Status} {Body}", resp.StatusCode, err);
            return null;
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OpenAiModels.ChatCompletionResponse>(body);
    }
}
