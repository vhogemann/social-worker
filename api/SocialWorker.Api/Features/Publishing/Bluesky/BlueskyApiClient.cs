using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed class BlueskyApiClient
{
    private const string CreateSessionEndpoint = "https://bsky.social/xrpc/com.atproto.server.createSession";
    private const string CreateRecordEndpoint = "https://bsky.social/xrpc/com.atproto.repo.createRecord";
    private const string UploadBlobEndpoint = "https://bsky.social/xrpc/com.atproto.repo.uploadBlob";

    private readonly HttpClient _http;

    public BlueskyApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<BlueskyApiCallResult<BlueskySession>> CreateSessionAsync(string identifier, string password, CancellationToken ct)
    {
        var req = new BlueskyCreateSessionRequest(identifier, password);
        var res = await _http.PostAsJsonAsync(CreateSessionEndpoint, req, ct);

        if (!res.IsSuccessStatusCode)
        {
            var error = await res.Content.ReadAsStringAsync(ct);
            return new BlueskyApiCallResult<BlueskySession>(false, Error: $"Failed to authenticate with Bluesky: {error}");
        }

        var data = await res.Content.ReadFromJsonAsync<BlueskyCreateSessionResponse>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(data?.AccessJwt) || string.IsNullOrWhiteSpace(data.Did))
        {
            return new BlueskyApiCallResult<BlueskySession>(false, Error: "Invalid session response from Bluesky.");
        }

        return new BlueskyApiCallResult<BlueskySession>(true, new BlueskySession(data.AccessJwt, data.Did));
    }

    public async Task<BlueskyApiCallResult<BlueskyRecordRef>> CreateRecordAsync(
        string did,
        BlueskyPostRecord record,
        string accessJwt,
        CancellationToken ct)
    {
        var req = new BlueskyCreateRecordRequest(did, "app.bsky.feed.post", record);
        using var message = new HttpRequestMessage(HttpMethod.Post, CreateRecordEndpoint)
        {
            Content = JsonContent.Create(req)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);

        var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var error = await res.Content.ReadAsStringAsync(ct);
            return new BlueskyApiCallResult<BlueskyRecordRef>(false, Error: $"Failed to post segment to Bluesky: {error}");
        }

        var data = await res.Content.ReadFromJsonAsync<BlueskyCreateRecordResponse>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(data?.Uri) || string.IsNullOrWhiteSpace(data.Cid))
        {
            return new BlueskyApiCallResult<BlueskyRecordRef>(false, Error: "Invalid post response from Bluesky.");
        }

        return new BlueskyApiCallResult<BlueskyRecordRef>(true, new BlueskyRecordRef(data.Uri, data.Cid));
    }

    public async Task<BlueskyApiCallResult<System.Text.Json.JsonElement>> UploadBlobAsync(
        byte[] bytes,
        string mimeType,
        string accessJwt,
        CancellationToken ct)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

        using var message = new HttpRequestMessage(HttpMethod.Post, UploadBlobEndpoint)
        {
            Content = content
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);

        var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            return new BlueskyApiCallResult<System.Text.Json.JsonElement>(false, Error: "Failed to upload blob to Bluesky.");
        }

        var data = await res.Content.ReadFromJsonAsync<BlueskyUploadBlobResponse>(cancellationToken: ct);
        if (data is null || data.Blob.ValueKind == System.Text.Json.JsonValueKind.Undefined)
        {
            return new BlueskyApiCallResult<System.Text.Json.JsonElement>(false, Error: "Invalid blob response from Bluesky.");
        }

        return new BlueskyApiCallResult<System.Text.Json.JsonElement>(true, data.Blob.Clone());
    }
}
