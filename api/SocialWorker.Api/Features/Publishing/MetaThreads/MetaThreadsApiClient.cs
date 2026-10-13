using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.MetaThreads;

public class MetaThreadsApiException : Exception
{
    public int? StatusCode { get; }
    public string? ErrorType { get; }
    public int? ErrorCode { get; }

    public MetaThreadsApiException(string message, int? statusCode = null, string? errorType = null, int? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        ErrorCode = errorCode;
    }
}

public class MetaThreadsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly MetaThreadsOptions _options;
    private readonly ILogger<MetaThreadsApiClient> _logger;

    public MetaThreadsApiClient(
        HttpClient httpClient,
        IOptions<MetaThreadsOptions> options,
        ILogger<MetaThreadsApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MetaThreadsContainerResponse> CreateTextContainerAsync(
        string userId,
        string accessToken,
        string text,
        string? replyToId = null,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["media_type"] = "TEXT",
            ["text"] = text,
            ["access_token"] = accessToken
        };

        if (!string.IsNullOrWhiteSpace(replyToId))
        {
            parameters["reply_to_id"] = replyToId;
        }

        return await PostContainerAsync(userId, parameters, ct);
    }

    public async Task<MetaThreadsContainerResponse> CreateImageContainerAsync(
        string userId,
        string accessToken,
        string imageUrl,
        string? text = null,
        string? altText = null,
        bool isCarouselItem = false,
        string? replyToId = null,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["media_type"] = "IMAGE",
            ["image_url"] = imageUrl,
            ["access_token"] = accessToken
        };

        if (isCarouselItem)
        {
            parameters["is_carousel_item"] = "true";
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parameters["text"] = text;
            }
            if (!string.IsNullOrWhiteSpace(replyToId))
            {
                parameters["reply_to_id"] = replyToId;
            }
        }

        if (!string.IsNullOrWhiteSpace(altText))
        {
            parameters["alt_text"] = altText;
        }

        return await PostContainerAsync(userId, parameters, ct);
    }

    public async Task<MetaThreadsContainerResponse> CreateCarouselContainerAsync(
        string userId,
        string accessToken,
        IReadOnlyList<string> children,
        string? text = null,
        string? replyToId = null,
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

        if (!string.IsNullOrWhiteSpace(text))
        {
            parameters["text"] = text;
        }

        if (!string.IsNullOrWhiteSpace(replyToId))
        {
            parameters["reply_to_id"] = replyToId;
        }

        return await PostContainerAsync(userId, parameters, ct);
    }

    public async Task<MetaThreadsContainerStatusResponse> GetContainerStatusAsync(
        string containerId,
        string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(containerId)}?fields=id,status,error_message&access_token={Uri.EscapeDataString(accessToken)}";
        var res = await _httpClient.GetAsync(url, ct);

        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        var statusResponse = JsonSerializer.Deserialize<MetaThreadsContainerStatusResponse>(json);
        if (statusResponse == null)
        {
            throw new MetaThreadsApiException("Failed to deserialize container status response.");
        }

        return statusResponse;
    }

    public async Task<bool> WaitForContainerReadyAsync(
        string containerId,
        string accessToken,
        TimeSpan? timeout = null,
        TimeSpan? initialDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delayFunc = null,
        CancellationToken ct = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(_options.StatusPollingTimeoutSeconds);
        var currentDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        delayFunc ??= Task.Delay;

        while (DateTime.UtcNow - startTime < effectiveTimeout)
        {
            var status = await GetContainerStatusAsync(containerId, accessToken, ct);

            if (string.Equals(status.Status, "FINISHED", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(status.Status, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                var error = status.ErrorMessage ?? "Threads media container failed processing.";
                throw new MetaThreadsApiException($"Threads media container processing failed: {error}");
            }

            if (string.Equals(status.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                throw new MetaThreadsApiException("Threads media container expired before publishing.");
            }

            await delayFunc(currentDelay, ct);
            if (currentDelay < maxDelay)
            {
                currentDelay = TimeSpan.FromMilliseconds(Math.Min(maxDelay.TotalMilliseconds, currentDelay.TotalMilliseconds * 1.5));
            }
        }

        throw new MetaThreadsApiException($"Timed out waiting for Threads media container '{containerId}' to become ready after {effectiveTimeout.TotalSeconds}s.");
    }

    public async Task<MetaThreadsPublishResponse> PublishContainerAsync(
        string userId,
        string accessToken,
        string containerId,
        CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(userId)}/threads_publish";
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
        var pubResponse = JsonSerializer.Deserialize<MetaThreadsPublishResponse>(json);
        if (pubResponse == null || string.IsNullOrWhiteSpace(pubResponse.Id))
        {
            throw new MetaThreadsApiException("Failed to deserialize Threads publish response or received empty ID.");
        }

        return pubResponse;
    }

    public virtual async Task<MetaThreadsRepliesResponse> GetRepliesAsync(
        string mediaId,
        string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(mediaId)}/replies?fields=id,text,timestamp,username,media_type,media_url,permalink,has_replies,replied_to,root_post&access_token={Uri.EscapeDataString(accessToken)}";
        var res = await _httpClient.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MetaThreadsRepliesResponse>(json) 
            ?? new MetaThreadsRepliesResponse();
    }

    public virtual async Task<MetaThreadsRepliesResponse> GetConversationAsync(
        string mediaId,
        string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(mediaId)}/conversation?fields=id,text,timestamp,username,media_type,media_url,permalink,has_replies,replied_to,root_post&access_token={Uri.EscapeDataString(accessToken)}";
        var res = await _httpClient.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MetaThreadsRepliesResponse>(json) 
            ?? new MetaThreadsRepliesResponse();
    }

    public virtual async Task<MetaThreadsUserThreadsResponse> GetUserThreadsAsync(
        string accessToken,
        int limit = 50,
        CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/me/threads?fields=id,media_type,text,permalink,shortcode&limit={limit}&access_token={Uri.EscapeDataString(accessToken)}";
        var res = await _httpClient.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MetaThreadsUserThreadsResponse>(json) 
            ?? new MetaThreadsUserThreadsResponse();
    }

    private async Task<MetaThreadsContainerResponse> PostContainerAsync(
        string userId,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(userId)}/threads";
        var form = new FormUrlEncodedContent(parameters);

        var res = await _httpClient.PostAsync(url, form, ct);
        if (!res.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(res, ct);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        var containerResponse = JsonSerializer.Deserialize<MetaThreadsContainerResponse>(json);
        if (containerResponse == null || string.IsNullOrWhiteSpace(containerResponse.Id))
        {
            throw new MetaThreadsApiException("Failed to deserialize Threads container response or received empty ID.");
        }

        return containerResponse;
    }

    private static async Task ThrowApiExceptionAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var content = await res.Content.ReadAsStringAsync(ct);
        try
        {
            var err = JsonSerializer.Deserialize<MetaThreadsApiErrorResponse>(content);
            if (err?.Error != null)
            {
                throw new MetaThreadsApiException(
                    err.Error.Message ?? "Threads API call failed.",
                    (int)res.StatusCode,
                    err.Error.Type,
                    err.Error.Code);
            }
        }
        catch (JsonException)
        {
        }

        throw new MetaThreadsApiException($"Threads API request failed with status {(int)res.StatusCode}: {content}", (int)res.StatusCode);
    }
}
