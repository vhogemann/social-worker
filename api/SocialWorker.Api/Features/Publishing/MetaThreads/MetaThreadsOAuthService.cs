using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.MetaThreads;

public class MetaThreadsOAuthService
{
    private readonly HttpClient _http;
    private readonly MetaThreadsOptions _options;
    private readonly ILogger<MetaThreadsOAuthService> _logger;

    public MetaThreadsOAuthService(
        HttpClient http,
        IOptions<MetaThreadsOptions> options,
        ILogger<MetaThreadsOAuthService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string stateNonce)
    {
        if (string.IsNullOrWhiteSpace(_options.AppId))
        {
            throw new InvalidOperationException("Meta Threads App ID is not configured.");
        }
        if (string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            throw new InvalidOperationException("Meta Threads redirect URI is not configured.");
        }

        var redirectUri = Uri.EscapeDataString(_options.RedirectUri);
        var scopes = Uri.EscapeDataString(_options.Scopes);
        var state = Uri.EscapeDataString(stateNonce);

        var baseUrl = _options.OAuthBaseUrl.TrimEnd('/') + "/authorize";
        return $"{baseUrl}?client_id={_options.AppId}&redirect_uri={redirectUri}&scope={scopes}&response_type=code&state={state}";
    }

    public async Task<MetaThreadsTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.AppId,
            ["client_secret"] = _options.AppSecret,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = code
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.threads.net/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(form)
        };

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Meta Threads code exchange failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Meta Threads token exchange failed: {ExtractErrorMessage(body)}");
        }

        var result = JsonSerializer.Deserialize<MetaThreadsTokenResponse>(body);
        if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new InvalidOperationException("Meta Threads token response was empty or missing access token.");
        }

        return result;
    }

    public async Task<MetaThreadsLongLivedTokenResponse> ExchangeForLongLivedTokenAsync(string shortLivedToken, CancellationToken ct = default)
    {
        var url = $"https://graph.threads.net/access_token?grant_type=th_exchange_token&client_secret={Uri.EscapeDataString(_options.AppSecret)}&access_token={Uri.EscapeDataString(shortLivedToken)}";
        var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Meta Threads long-lived token exchange failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Meta Threads long-lived exchange failed: {ExtractErrorMessage(body)}");
        }

        var result = JsonSerializer.Deserialize<MetaThreadsLongLivedTokenResponse>(body);
        if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new InvalidOperationException("Meta Threads long-lived token response was empty.");
        }

        return result;
    }

    public async Task<MetaThreadsLongLivedTokenResponse> RefreshTokenAsync(string longLivedToken, CancellationToken ct = default)
    {
        var url = $"https://graph.threads.net/refresh_access_token?grant_type=th_refresh_token&access_token={Uri.EscapeDataString(longLivedToken)}";
        var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Meta Threads token refresh failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Meta Threads token refresh failed: {ExtractErrorMessage(body)}");
        }

        var result = JsonSerializer.Deserialize<MetaThreadsLongLivedTokenResponse>(body);
        if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new InvalidOperationException("Meta Threads token refresh response was empty.");
        }

        return result;
    }

    public async Task<MetaThreadsUserProfile> FetchUserProfileAsync(string accessToken, CancellationToken ct = default)
    {
        var url = $"https://graph.threads.net/v1.0/me?fields=id,username,threads_profile_picture_url&access_token={Uri.EscapeDataString(accessToken)}";
        var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Meta Threads profile lookup failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Meta Threads profile lookup failed: {ExtractErrorMessage(body)}");
        }

        var result = JsonSerializer.Deserialize<MetaThreadsUserProfile>(body);
        if (result == null || string.IsNullOrWhiteSpace(result.Id))
        {
            throw new InvalidOperationException("Meta Threads profile response was empty or missing user ID.");
        }

        return result;
    }

    private static string ExtractErrorMessage(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<MetaThreadsApiErrorResponse>(json);
            if (!string.IsNullOrWhiteSpace(parsed?.Error?.Message))
            {
                return parsed.Error.Message;
            }
        }
        catch
        {
        }
        return json;
    }
}
