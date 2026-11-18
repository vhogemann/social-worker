using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.Mastodon;

public sealed record MastodonApiCallResult<T>(
    bool Success,
    T? Data = default,
    string? Error = null,
    HttpStatusCode? StatusCode = null);

public sealed class MastodonApiClient
{
    private readonly HttpClient _http;
    private readonly MastodonOptions _options;

    public MastodonApiClient(HttpClient http, IOptions<MastodonOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<MastodonApiCallResult<MastodonMediaUploadResponse>> UploadMediaAsync(
        string instanceUrl,
        string accessToken,
        byte[] bytes,
        string fileName,
        string contentType,
        string? altText,
        CancellationToken ct = default)
    {
        var baseUrl = instanceUrl.TrimEnd('/');
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "upload" : Path.GetFileName(fileName));

        if (!string.IsNullOrWhiteSpace(altText))
        {
            form.Add(new StringContent(altText), "description");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v2/media")
        {
            Content = form
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return new MastodonApiCallResult<MastodonMediaUploadResponse>(
                false,
                Error: $"Failed to upload media to Mastodon ({(int)res.StatusCode}): {body}",
                StatusCode: res.StatusCode);
        }

        var media = await res.Content.ReadFromJsonAsync<MastodonMediaUploadResponse>(cancellationToken: ct);
        if (media is null || string.IsNullOrWhiteSpace(media.Id))
        {
            return new MastodonApiCallResult<MastodonMediaUploadResponse>(
                false,
                Error: "Mastodon returned an invalid media upload response.",
                StatusCode: res.StatusCode);
        }

        if (res.StatusCode == HttpStatusCode.Accepted || string.IsNullOrWhiteSpace(media.Url))
        {
            return await PollMediaUntilReadyAsync(baseUrl, accessToken, media.Id, ct);
        }

        return new MastodonApiCallResult<MastodonMediaUploadResponse>(true, media, StatusCode: res.StatusCode);
    }

    private async Task<MastodonApiCallResult<MastodonMediaUploadResponse>> PollMediaUntilReadyAsync(
        string baseUrl,
        string accessToken,
        string mediaId,
        CancellationToken ct)
    {
        var timeoutSeconds = _options.MediaPollingTimeoutSeconds > 0 ? _options.MediaPollingTimeoutSeconds : 120;
        var intervalMs = _options.MediaPollingIntervalMs > 0 ? _options.MediaPollingIntervalMs : 1000;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(intervalMs, ct);

            using var message = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/media/{Uri.EscapeDataString(mediaId)}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var res = await _http.SendAsync(message, ct);
            if (res.StatusCode == HttpStatusCode.PartialContent)
            {
                continue;
            }

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                return new MastodonApiCallResult<MastodonMediaUploadResponse>(
                    false,
                    Error: $"Failed while polling Mastodon media processing ({(int)res.StatusCode}): {body}",
                    StatusCode: res.StatusCode);
            }

            var media = await res.Content.ReadFromJsonAsync<MastodonMediaUploadResponse>(cancellationToken: ct);
            if (media is not null && !string.IsNullOrWhiteSpace(media.Url))
            {
                return new MastodonApiCallResult<MastodonMediaUploadResponse>(true, media, StatusCode: res.StatusCode);
            }
        }

        return new MastodonApiCallResult<MastodonMediaUploadResponse>(
            false,
            Error: $"Timed out waiting for Mastodon media {mediaId} to finish processing.");
    }

    public async Task<MastodonApiCallResult<MastodonStatusResponse>> CreateStatusAsync(
        string instanceUrl,
        string accessToken,
        MastodonStatusRequest request,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var baseUrl = instanceUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/statuses")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        using var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return new MastodonApiCallResult<MastodonStatusResponse>(
                false,
                Error: $"Failed to publish status to Mastodon ({(int)res.StatusCode}): {body}",
                StatusCode: res.StatusCode);
        }

        var status = await res.Content.ReadFromJsonAsync<MastodonStatusResponse>(cancellationToken: ct);
        if (status is null || string.IsNullOrWhiteSpace(status.Id))
        {
            return new MastodonApiCallResult<MastodonStatusResponse>(
                false,
                Error: "Mastodon returned an invalid status response.",
                StatusCode: res.StatusCode);
        }

        return new MastodonApiCallResult<MastodonStatusResponse>(true, status, StatusCode: res.StatusCode);
    }

    public async Task<MastodonApiCallResult<MastodonStatusResponse>> GetStatusAsync(
        string instanceUrl,
        string? accessToken,
        string statusId,
        CancellationToken ct = default)
    {
        var baseUrl = instanceUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/statuses/{Uri.EscapeDataString(statusId)}");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return new MastodonApiCallResult<MastodonStatusResponse>(
                false,
                Error: $"Failed to fetch Mastodon status ({(int)res.StatusCode}): {body}",
                StatusCode: res.StatusCode);
        }

        var status = await res.Content.ReadFromJsonAsync<MastodonStatusResponse>(cancellationToken: ct);
        if (status is null)
        {
            return new MastodonApiCallResult<MastodonStatusResponse>(false, Error: "Mastodon returned an empty status.", StatusCode: res.StatusCode);
        }

        return new MastodonApiCallResult<MastodonStatusResponse>(true, status, StatusCode: res.StatusCode);
    }

    public async Task<MastodonApiCallResult<MastodonStatusContextResponse>> GetStatusContextAsync(
        string instanceUrl,
        string accessToken,
        string statusId,
        CancellationToken ct = default)
    {
        var baseUrl = instanceUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/statuses/{Uri.EscapeDataString(statusId)}/context");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return new MastodonApiCallResult<MastodonStatusContextResponse>(
                false,
                Error: $"Failed to fetch Mastodon status context ({(int)res.StatusCode}): {body}",
                StatusCode: res.StatusCode);
        }

        var context = await res.Content.ReadFromJsonAsync<MastodonStatusContextResponse>(cancellationToken: ct);
        if (context is null)
        {
            return new MastodonApiCallResult<MastodonStatusContextResponse>(false, Error: "Mastodon returned an empty context.", StatusCode: res.StatusCode);
        }

        return new MastodonApiCallResult<MastodonStatusContextResponse>(true, context, StatusCode: res.StatusCode);
    }

    public async Task<MastodonApiCallResult<MastodonStatusResponse>> FavouriteStatusAsync(
        string instanceUrl,
        string accessToken,
        string statusId,
        CancellationToken ct = default)
    {
        var baseUrl = instanceUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/statuses/{Uri.EscapeDataString(statusId)}/favourite");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return new MastodonApiCallResult<MastodonStatusResponse>(
                false,
                Error: $"Failed to favourite Mastodon status ({(int)res.StatusCode}): {body}",
                StatusCode: res.StatusCode);
        }

        var status = await res.Content.ReadFromJsonAsync<MastodonStatusResponse>(cancellationToken: ct);
        return new MastodonApiCallResult<MastodonStatusResponse>(true, status, StatusCode: res.StatusCode);
    }
}
