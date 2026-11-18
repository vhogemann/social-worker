using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Sources.Instagram;

namespace SocialWorker.Api.Features.Publishing.Instagram;

public class InstagramApiClient
{
    private readonly HttpClient _httpClient;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramApiClient> _logger;

    public InstagramApiClient(
        HttpClient httpClient,
        IOptions<InstagramOptions> options,
        ILogger<InstagramApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string ResolveGraphBaseUrl()
    {
        if (_options.OAuthBaseUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            var igBase = _options.InstagramGraphBaseUrl.TrimEnd('/');
            return igBase.EndsWith("/v21.0", StringComparison.OrdinalIgnoreCase) || igBase.EndsWith("/v20.0", StringComparison.OrdinalIgnoreCase)
                ? igBase + "/"
                : igBase + "/v21.0/";
        }

        return _options.BaseUrl.TrimEnd('/') + "/";
    }

    public virtual async Task<InstagramContainerResponse> CreateImageContainerAsync(
        string igUserId,
        string accessToken,
        string imageUrl,
        string? caption = null,
        string? altText = null,
        bool isCarouselItem = false,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["access_token"] = accessToken
        };

        if (isCarouselItem)
        {
            parameters["is_carousel_item"] = "true";
        }
        else if (!string.IsNullOrWhiteSpace(caption))
        {
            parameters["caption"] = caption;
        }

        if (!string.IsNullOrWhiteSpace(altText))
        {
            parameters["alt_text"] = altText;
        }

        return await PostContainerAsync(igUserId, parameters, ct);
    }

    public virtual async Task<InstagramContainerResponse> CreateCarouselContainerAsync(
        string igUserId,
        string accessToken,
        IReadOnlyList<string> children,
        string? caption = null,
        CancellationToken ct = default)
    {
        if (children == null || children.Count < 2 || children.Count > 10)
        {
            throw new ArgumentException("A carousel container requires between 2 and 10 child item IDs.", nameof(children));
        }

        var parameters = new Dictionary<string, string>
        {
            ["media_type"] = "CAROUSEL",
            ["children"] = string.Join(",", children),
            ["access_token"] = accessToken
        };

        if (!string.IsNullOrWhiteSpace(caption))
        {
            parameters["caption"] = caption;
        }

        return await PostContainerAsync(igUserId, parameters, ct);
    }

    public virtual async Task<InstagramContainerStatusResponse> GetContainerStatusAsync(
        string containerId,
        string accessToken,
        CancellationToken ct = default)
    {
        var baseUrl = ResolveGraphBaseUrl();
        var url = $"{baseUrl}{Uri.EscapeDataString(containerId)}?fields=id,status_code,status&access_token={Uri.EscapeDataString(accessToken)}";
        var res = await _httpClient.GetAsync(url, ct);

        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        var statusResponse = JsonSerializer.Deserialize<InstagramContainerStatusResponse>(json);
        if (statusResponse == null)
        {
            throw new InstagramApiException("Failed to deserialize Instagram container status response.");
        }

        return statusResponse;
    }

    public virtual async Task<bool> WaitForContainerReadyAsync(
        string containerId,
        string accessToken,
        TimeSpan? timeout = null,
        TimeSpan? initialDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delayFunc = null,
        CancellationToken ct = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(_options.StatusPollingTimeoutSeconds > 0 ? _options.StatusPollingTimeoutSeconds : 120);
        var currentDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(_options.MaxStatusPollIntervalSeconds > 0 ? _options.MaxStatusPollIntervalSeconds : 30);
        var startTime = DateTime.UtcNow;
        delayFunc ??= Task.Delay;

        while (DateTime.UtcNow - startTime < effectiveTimeout)
        {
            var status = await GetContainerStatusAsync(containerId, accessToken, ct);

            if (string.Equals(status.StatusCode, "FINISHED", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(status.StatusCode, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                var error = status.Status ?? "Instagram media container failed processing.";
                throw new InstagramApiException($"Instagram media container processing failed: {error}");
            }

            if (string.Equals(status.StatusCode, "EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InstagramApiException("Instagram media container expired before publishing.");
            }

            await delayFunc(currentDelay, ct);
            if (currentDelay < maxDelay)
            {
                currentDelay = TimeSpan.FromMilliseconds(Math.Min(maxDelay.TotalMilliseconds, currentDelay.TotalMilliseconds * 2));
            }
        }

        throw new InstagramApiException($"Timed out waiting for Instagram media container '{containerId}' to become ready after {effectiveTimeout.TotalSeconds}s.");
    }

    public virtual async Task<InstagramPublishResponse> PublishContainerAsync(
        string igUserId,
        string accessToken,
        string containerId,
        CancellationToken ct = default)
    {
        var baseUrl = ResolveGraphBaseUrl();
        var url = $"{baseUrl}{Uri.EscapeDataString(igUserId)}/media_publish";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["creation_id"] = containerId,
            ["access_token"] = accessToken
        });

        var res = await _httpClient.PostAsync(url, form, ct);
        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        var pubResponse = JsonSerializer.Deserialize<InstagramPublishResponse>(json);
        if (pubResponse == null || string.IsNullOrWhiteSpace(pubResponse.Id))
        {
            throw new InstagramApiException("Failed to deserialize Instagram publish response or received empty ID.");
        }

        return pubResponse;
    }

    public virtual async Task<InstagramMediaPermalinkResponse?> GetMediaPermalinkAsync(
        string mediaId,
        string accessToken,
        CancellationToken ct = default)
    {
        try
        {
            var baseUrl = ResolveGraphBaseUrl();
            var url = $"{baseUrl}{Uri.EscapeDataString(mediaId)}?fields=id,permalink,shortcode&access_token={Uri.EscapeDataString(accessToken)}";
            var res = await _httpClient.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<InstagramMediaPermalinkResponse>(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Best-effort permalink lookup failed for Instagram media {MediaId}", mediaId);
            return null;
        }
    }

    private async Task<InstagramContainerResponse> PostContainerAsync(
        string igUserId,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        var baseUrl = ResolveGraphBaseUrl();
        var url = $"{baseUrl}{Uri.EscapeDataString(igUserId)}/media";
        var form = new FormUrlEncodedContent(parameters);
        var res = await _httpClient.PostAsync(url, form, ct);

        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        var containerResponse = JsonSerializer.Deserialize<InstagramContainerResponse>(json);
        if (containerResponse == null || string.IsNullOrWhiteSpace(containerResponse.Id))
        {
            throw new InstagramApiException("Failed to deserialize Instagram container response or received empty ID.");
        }

        return containerResponse;
    }

    private async Task ThrowApiExceptionAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var body = await res.Content.ReadAsStringAsync(ct);
        string message = $"Instagram API returned {(int)res.StatusCode}";
        string? errorType = null;
        int? errorCode = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorElem))
            {
                if (errorElem.TryGetProperty("message", out var msgElem))
                {
                    message = msgElem.GetString() ?? message;
                }
                if (errorElem.TryGetProperty("type", out var typeElem))
                {
                    errorType = typeElem.GetString();
                }
                if (errorElem.TryGetProperty("code", out var codeElem) && codeElem.TryGetInt32(out var codeInt))
                {
                    errorCode = codeInt;
                }
            }
            else if (doc.RootElement.TryGetProperty("error_message", out var errMsgElem))
            {
                message = errMsgElem.GetString() ?? message;
            }
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                message = $"{message}: {body}";
            }
        }

        _logger.LogWarning("Instagram API error ({StatusCode}, Code={ErrorCode}, Type={ErrorType}): {Message}",
            (int)res.StatusCode, errorCode, errorType, message);
        throw new InstagramApiException(message, (int)res.StatusCode, errorType, errorCode);
    }
}
