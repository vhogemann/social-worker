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
        IReadOnlyList<string>? mediaIds = null,
        CancellationToken ct = default)
    {
        var url = CombineUrl(_options.BaseUrl, "tweets");
        var payload = new TwitterCreateTweetRequest(
            Text: text,
            Reply: !string.IsNullOrWhiteSpace(inReplyToTweetId) ? new TwitterReplyOptions(inReplyToTweetId) : null,
            Media: mediaIds != null && mediaIds.Count > 0 ? new TwitterMediaOptions(mediaIds) : null);

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

    public virtual async Task<TwitterSearchTweetsResponse?> SearchRecentConversationAsync(
        string accessToken,
        string conversationId,
        DateTime? sinceUtc = null,
        CancellationToken ct = default)
    {
        var query = $"conversation_id:{conversationId} -is:retweet";
        var queryEscaped = Uri.EscapeDataString(query);
        var url = CombineUrl(_options.BaseUrl, $"tweets/search/recent?query={queryEscaped}&tweet.fields=author_id,created_at,in_reply_to_user_id,conversation_id&expansions=author_id&user.fields=username,name");

        if (sinceUtc.HasValue)
        {
            var startIso = Uri.EscapeDataString(sinceUtc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            url += $"&start_time={startIso}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Twitter search recent failed ({StatusCode}): {Body}", response.StatusCode, body);
            var errorMsg = ExtractErrorMessage(body);
            throw new InvalidOperationException($"Twitter API returned {(int)response.StatusCode}: {errorMsg}");
        }

        return JsonSerializer.Deserialize<TwitterSearchTweetsResponse>(body);
    }

    public async Task<string> UploadMediaAsync(
        string accessToken,
        byte[] mediaBytes,
        string mimeType,
        CancellationToken ct = default)
    {
        var category = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? "tweet_video"
            : mimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase)
                ? "tweet_gif"
                : "tweet_image";

        var initUrl = CombineUrl(_options.MediaUploadBaseUrl, "initialize");
        var initPayload = new TwitterMediaInitRequest(
            TotalBytes: mediaBytes.Length,
            MediaType: mimeType,
            MediaCategory: category);

        var initJson = JsonSerializer.Serialize(initPayload);
        var initRequest = new HttpRequestMessage(HttpMethod.Post, initUrl)
        {
            Content = new StringContent(initJson, Encoding.UTF8, "application/json")
        };
        initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var initResponse = await _http.SendAsync(initRequest, ct);
        var initBody = await initResponse.Content.ReadAsStringAsync(ct);
        if (!initResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Twitter media init failed ({StatusCode}): {Body}", initResponse.StatusCode, initBody);
            throw new InvalidOperationException($"Twitter media init failed: {ExtractErrorMessage(initBody)}");
        }

        var initResult = JsonSerializer.Deserialize<TwitterMediaInitResponse>(initBody);
        var mediaId = initResult?.Data?.Id;
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            using var doc = JsonDocument.Parse(initBody);
            if (doc.RootElement.TryGetProperty("media_id_string", out var idStr))
            {
                mediaId = idStr.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new InvalidOperationException("Twitter returned an empty media ID during initialization.");
        }

        var appendUrl = CombineUrl(_options.MediaUploadBaseUrl, $"{mediaId}/append");
        using var form = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(mediaBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(byteContent, "media", "upload");
        form.Add(new StringContent("0"), "segment_index");

        var appendRequest = new HttpRequestMessage(HttpMethod.Post, appendUrl)
        {
            Content = form
        };
        appendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var appendResponse = await _http.SendAsync(appendRequest, ct);
        if (!appendResponse.IsSuccessStatusCode)
        {
            var appendBody = await appendResponse.Content.ReadAsStringAsync(ct);
            _logger.LogError("Twitter media append failed ({StatusCode}): {Body}", appendResponse.StatusCode, appendBody);
            throw new InvalidOperationException($"Twitter media append failed: {ExtractErrorMessage(appendBody)}");
        }

        var finalizeUrl = CombineUrl(_options.MediaUploadBaseUrl, $"{mediaId}/finalize");
        var finalizeRequest = new HttpRequestMessage(HttpMethod.Post, finalizeUrl);
        finalizeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var finalizeResponse = await _http.SendAsync(finalizeRequest, ct);
        if (!finalizeResponse.IsSuccessStatusCode)
        {
            var finalizeBody = await finalizeResponse.Content.ReadAsStringAsync(ct);
            _logger.LogError("Twitter media finalize failed ({StatusCode}): {Body}", finalizeResponse.StatusCode, finalizeBody);
            throw new InvalidOperationException($"Twitter media finalize failed: {ExtractErrorMessage(finalizeBody)}");
        }

        return mediaId;
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
