using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Sources.Instagram;

public class InstagramOAuthService
{
    private readonly HttpClient _http;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramOAuthService> _logger;

    public InstagramOAuthService(
        HttpClient http,
        IOptions<InstagramOptions> options,
        ILogger<InstagramOAuthService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string stateNonce)
    {
        if (string.IsNullOrWhiteSpace(_options.AppId))
            throw new InvalidOperationException("Instagram/Meta App ID is not configured.");
        if (string.IsNullOrWhiteSpace(_options.RedirectUri))
            throw new InvalidOperationException("Instagram/Meta redirect URI is not configured.");

        var redirectUri = Uri.EscapeDataString(_options.RedirectUri);
        var scopes = Uri.EscapeDataString(_options.Scopes);
        var state = Uri.EscapeDataString(stateNonce);

        var baseUrl = _options.OAuthBaseUrl.TrimEnd('/');
        return $"{baseUrl}?client_id={_options.AppId}&redirect_uri={redirectUri}&scope={scopes}&response_type=code&state={state}";
    }

    public async Task<InstagramTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        if (_options.OAuthBaseUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = _options.AppId,
                ["client_secret"] = _options.AppSecret,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = _options.RedirectUri,
                ["code"] = code
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.instagram.com/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(form)
            };
            var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram code exchange failed ({StatusCode}): {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Instagram code exchange failed: {ExtractErrorMessage(body)}");
            }

            var result = JsonSerializer.Deserialize<InstagramTokenResponse>(body);
            if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                throw new InvalidOperationException("Instagram token response was empty or missing access token.");
            }

            return result;
        }
        else
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
            var url = $"{baseUrl}oauth/access_token?client_id={Uri.EscapeDataString(_options.AppId)}&client_secret={Uri.EscapeDataString(_options.AppSecret)}&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}&code={Uri.EscapeDataString(code)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram code exchange failed ({StatusCode}): {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Instagram code exchange failed: {ExtractErrorMessage(body)}");
            }

            var result = JsonSerializer.Deserialize<InstagramTokenResponse>(body);
            if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                throw new InvalidOperationException("Instagram token response was empty or missing access token.");
            }

            return result;
        }
    }

    public async Task<InstagramTokenResponse> ExchangeForLongLivedTokenAsync(string shortToken, CancellationToken ct = default)
    {
        string url;
        if (_options.OAuthBaseUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            var igBase = _options.InstagramGraphBaseUrl.TrimEnd('/') + "/";
            url = $"{igBase}access_token?grant_type=ig_exchange_token&client_secret={Uri.EscapeDataString(_options.AppSecret)}&access_token={Uri.EscapeDataString(shortToken)}";
        }
        else
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
            url = $"{baseUrl}oauth/access_token?grant_type=fb_exchange_token&client_id={Uri.EscapeDataString(_options.AppId)}&client_secret={Uri.EscapeDataString(_options.AppSecret)}&fb_exchange_token={Uri.EscapeDataString(shortToken)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Instagram long-lived token exchange failed ({StatusCode}): {Body}. Falling back to short-lived token.", response.StatusCode, body);
            return new InstagramTokenResponse(shortToken, "Bearer", 3600);
        }

        var result = JsonSerializer.Deserialize<InstagramTokenResponse>(body);
        return result ?? new InstagramTokenResponse(shortToken, "Bearer", 3600);
    }

    public async Task<(string IgUserId, string Username, string? PageToken)> DiscoverInstagramAccountAsync(string userToken, CancellationToken ct = default)
    {
        if (_options.OAuthBaseUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            var igBase = _options.InstagramGraphBaseUrl.TrimEnd('/') + "/";
            var meUrl = $"{igBase}v21.0/me?fields=id,username&access_token={Uri.EscapeDataString(userToken)}";
            using var meRequest = new HttpRequestMessage(HttpMethod.Get, meUrl);
            var meResponse = await _http.SendAsync(meRequest, ct);

            if (meResponse.IsSuccessStatusCode)
            {
                var meBody = await meResponse.Content.ReadAsStringAsync(ct);
                var meResult = JsonSerializer.Deserialize<MetaMeInstagramResponse>(meBody);
                if (!string.IsNullOrWhiteSpace(meResult?.InstagramBusinessAccount?.Id))
                {
                    return (meResult.InstagramBusinessAccount.Id, meResult.InstagramBusinessAccount.Username ?? meResult.Username ?? meResult.InstagramBusinessAccount.Id, userToken);
                }
                if (!string.IsNullOrWhiteSpace(meResult?.Id))
                {
                    return (meResult.Id, meResult.Username ?? meResult.Id, userToken);
                }
            }
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";

        var accountsUrl = $"{baseUrl}me/accounts?fields=id,name,access_token,instagram_business_account{{id,username,name,profile_picture_url}}&access_token={Uri.EscapeDataString(userToken)}";
        using var accountsRequest = new HttpRequestMessage(HttpMethod.Get, accountsUrl);
        var accountsResponse = await _http.SendAsync(accountsRequest, ct);

        if (accountsResponse.IsSuccessStatusCode)
        {
            var accountsBody = await accountsResponse.Content.ReadAsStringAsync(ct);
            var accountsResult = JsonSerializer.Deserialize<MetaAccountsResponse>(accountsBody);
            var page = accountsResult?.Data?.FirstOrDefault(p => p.InstagramBusinessAccount != null);
            if (page?.InstagramBusinessAccount != null)
            {
                var tokenToUse = !string.IsNullOrWhiteSpace(page.AccessToken) ? page.AccessToken : userToken;
                return (page.InstagramBusinessAccount.Id, page.InstagramBusinessAccount.Username ?? page.InstagramBusinessAccount.Id, tokenToUse);
            }
        }

        var fbMeUrl = $"{baseUrl}me?fields=id,name,username,instagram_business_account{{id,username,name,profile_picture_url}}&access_token={Uri.EscapeDataString(userToken)}";
        using var fbMeRequest = new HttpRequestMessage(HttpMethod.Get, fbMeUrl);
        var fbMeResponse = await _http.SendAsync(fbMeRequest, ct);

        if (fbMeResponse.IsSuccessStatusCode)
        {
            var meBody = await fbMeResponse.Content.ReadAsStringAsync(ct);
            var meResult = JsonSerializer.Deserialize<MetaMeInstagramResponse>(meBody);
            if (meResult?.InstagramBusinessAccount != null)
            {
                return (meResult.InstagramBusinessAccount.Id, meResult.InstagramBusinessAccount.Username ?? meResult.InstagramBusinessAccount.Id, userToken);
            }
            if (!string.IsNullOrWhiteSpace(meResult?.Id))
            {
                return (meResult.Id, meResult.Username ?? meResult.Id, userToken);
            }
        }

        throw new InvalidOperationException("No Instagram Professional/Business account found linked to this Meta account. Please ensure your Instagram account is switched to a Creator or Business account and connected to your Facebook Page.");
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
