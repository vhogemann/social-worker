using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public sealed record LinkedInApiCallResult<T>(bool Success, T? Value = default, string? Error = null);

public sealed class LinkedInApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<LinkedInApiClient>? _logger;

    public LinkedInApiClient(HttpClient http, ILogger<LinkedInApiClient>? logger = null)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<LinkedInApiCallResult<LinkedInRegisterUploadResponse>> RegisterUploadAsync(
        string ownerUrn,
        string token,
        CancellationToken ct)
    {
        var req = new LinkedInRegisterUploadRequest
        {
            RegisterUploadRequest = new LinkedInRegisterRequest
            {
                Owner = ownerUrn
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/v2/assets?action=registerUpload");
        message.Content = JsonContent.Create(req);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");

        var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning("LinkedIn registerUpload failed with {StatusCode}: {ErrorBody}", (int)res.StatusCode, err);
            return new LinkedInApiCallResult<LinkedInRegisterUploadResponse>(false, Error: $"Failed to register image upload: {err}");
        }

        var data = await res.Content.ReadFromJsonAsync<LinkedInRegisterUploadResponse>(cancellationToken: ct);
        if (data?.Value?.Asset == null || data.Value.UploadMechanism?.MediaUploadHttpRequest?.UploadUrl == null)
        {
            _logger?.LogWarning("LinkedIn registerUpload returned an invalid response payload.");
            return new LinkedInApiCallResult<LinkedInRegisterUploadResponse>(false, Error: "Invalid upload registration response from LinkedIn.");
        }

        return new LinkedInApiCallResult<LinkedInRegisterUploadResponse>(true, data);
    }

    public async Task<LinkedInApiCallResult<bool>> UploadBinaryAsync(
        string uploadUrl,
        byte[] bytes,
        string mimeType,
        string token,
        Dictionary<string, string>? headers,
        CancellationToken ct)
    {
        var contentType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType;
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var message = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        message.Content = content;

        var hasAuthorizationHeader = false;
        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    hasAuthorizationHeader = true;
                }
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!hasAuthorizationHeader && !string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning("LinkedIn binary image upload failed with {StatusCode}: {ErrorBody}", (int)res.StatusCode, err);
            return new LinkedInApiCallResult<bool>(false, Error: $"Binary upload to LinkedIn failed: {err}");
        }

        return new LinkedInApiCallResult<bool>(true, true);
    }

    public async Task<LinkedInApiCallResult<string>> PublishPostAsync(
        LinkedInUgcPostRequest req,
        string token,
        CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/v2/ugcPosts");
        message.Content = JsonContent.Create(req);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");

        var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning("LinkedIn ugcPosts publish failed with {StatusCode}: {ErrorBody}", (int)res.StatusCode, err);
            return new LinkedInApiCallResult<string>(false, Error: $"LinkedIn publish request failed: {err}");
        }

        var data = await res.Content.ReadFromJsonAsync<LinkedInUgcPostResponse>(cancellationToken: ct);
        if (!string.IsNullOrEmpty(data?.Id))
        {
            return new LinkedInApiCallResult<string>(true, data.Id);
        }

        if (res.Headers.Location != null)
        {
            var segments = res.Headers.Location.Segments;
            if (segments.Length > 0)
            {
                var lastSegment = Uri.UnescapeDataString(segments[^1]).Trim('/');
                return new LinkedInApiCallResult<string>(true, lastSegment);
            }
        }

        return new LinkedInApiCallResult<string>(false, Error: "LinkedIn post ID was not returned in the publish response.");
    }
}
