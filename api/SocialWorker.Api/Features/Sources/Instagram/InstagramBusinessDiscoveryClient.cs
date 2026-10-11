using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Sources.Instagram;

public class InstagramBusinessDiscoveryClient : IInstagramDiscoveryClient
{
    private readonly HttpClient _http;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramBusinessDiscoveryClient> _logger;

    public InstagramBusinessDiscoveryClient(
        HttpClient http,
        IOptions<InstagramOptions> options,
        ILogger<InstagramBusinessDiscoveryClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InstagramProfileDiscovery?> GetProfileWithMediaAsync(
        string callerIgUserId,
        string targetUsername,
        string accessToken,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(callerIgUserId))
            throw new ArgumentException("Caller Instagram User ID is required.", nameof(callerIgUserId));
        if (string.IsNullOrWhiteSpace(targetUsername))
            throw new ArgumentException("Target username is required.", nameof(targetUsername));
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token is required.", nameof(accessToken));

        var cleanTarget = targetUsername.Trim().Trim('@', '/');
        var cleanLimit = Math.Clamp(limit, 1, 50);

        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        var fields = $"business_discovery.username({cleanTarget}){{id,username,name,profile_picture_url,biography,media.limit({cleanLimit}){{id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,like_count,comments_count}}}}";
        var requestUrl = $"{baseUrl}{callerIgUserId}?fields={Uri.EscapeDataString(fields)}&access_token={Uri.EscapeDataString(accessToken)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Instagram Business Discovery failed for @{Target} ({StatusCode}): {Body}", cleanTarget, response.StatusCode, body);
            var errorMessage = ExtractErrorMessage(body);
            throw new InvalidOperationException($"Instagram Business Discovery failed for @{cleanTarget}: {errorMessage}");
        }

        var result = JsonSerializer.Deserialize<InstagramBusinessDiscoveryRootResponse>(body);
        if (result?.Error != null)
        {
            _logger.LogError("Instagram Graph API returned error for @{Target}: {Message}", cleanTarget, result.Error.Message);
            throw new InvalidOperationException($"Instagram API error for @{cleanTarget}: {result.Error.Message}");
        }

        return result?.BusinessDiscovery;
    }

    public async Task<Feed> GetProfileFeedAsync(
        string targetUsername,
        string callerIgUserId,
        string accessToken,
        int limit = 10,
        CancellationToken ct = default)
    {
        var profile = await GetProfileWithMediaAsync(callerIgUserId, targetUsername, accessToken, limit, ct);
        if (profile == null)
        {
            throw new InvalidOperationException($"Instagram profile not found for @{targetUsername}");
        }

        return InstagramFeedConverter.ToFeed(profile);
    }

    public async Task<string?> ResolveCallerIgUserIdAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";

        try
        {
            var meUrl = $"{baseUrl}me?fields=instagram_business_account{{id,username,name}}&access_token={Uri.EscapeDataString(accessToken)}";
            using var meRequest = new HttpRequestMessage(HttpMethod.Get, meUrl);
            var meResponse = await _http.SendAsync(meRequest, ct);
            if (meResponse.IsSuccessStatusCode)
            {
                var meBody = await meResponse.Content.ReadAsStringAsync(ct);
                var meResult = JsonSerializer.Deserialize<MetaMeInstagramResponse>(meBody);
                if (meResult?.InstagramBusinessAccount?.Id != null)
                {
                    return meResult.InstagramBusinessAccount.Id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve IG account via /me endpoint.");
        }

        try
        {
            var accountsUrl = $"{baseUrl}me/accounts?fields=id,name,access_token,instagram_business_account{{id,username,name}}&access_token={Uri.EscapeDataString(accessToken)}";
            using var accountsRequest = new HttpRequestMessage(HttpMethod.Get, accountsUrl);
            var accountsResponse = await _http.SendAsync(accountsRequest, ct);
            if (accountsResponse.IsSuccessStatusCode)
            {
                var accountsBody = await accountsResponse.Content.ReadAsStringAsync(ct);
                var accountsResult = JsonSerializer.Deserialize<MetaAccountsResponse>(accountsBody);
                var firstWithIg = accountsResult?.Data?.FirstOrDefault(p => p.InstagramBusinessAccount != null);
                if (firstWithIg?.InstagramBusinessAccount?.Id != null)
                {
                    return firstWithIg.InstagramBusinessAccount.Id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve IG account via /me/accounts endpoint.");
        }

        return null;
    }

    private static string ExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                if (errorEl.TryGetProperty("message", out var msgEl))
                {
                    return msgEl.GetString() ?? "Unknown error";
                }
            }
        }
        catch
        {
        }

        return json.Length > 200 ? json[..200] + "..." : json;
    }
}
