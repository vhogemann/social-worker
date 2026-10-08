using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.Twitter;

public sealed record TwitterPkceState(string CodeVerifier);

public class TwitterOAuthService
{
    private readonly HttpClient _http;
    private readonly TwitterOptions _options;
    private readonly ILogger<TwitterOAuthService> _logger;

    public TwitterOAuthService(
        HttpClient http,
        IOptions<TwitterOptions> options,
        ILogger<TwitterOAuthService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    public string GetAuthorizationUrl(string stateNonce, string codeChallenge)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Twitter Client ID is not configured.");
        }
        if (string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            throw new InvalidOperationException("Twitter redirect URI is not configured.");
        }

        var redirectUri = Uri.EscapeDataString(_options.RedirectUri);
        var scopes = Uri.EscapeDataString(_options.Scopes);
        var state = Uri.EscapeDataString(stateNonce);
        var challenge = Uri.EscapeDataString(codeChallenge);

        var baseUrl = _options.OAuthBaseUrl.TrimEnd('/') + "/authorize";
        return $"{baseUrl}?response_type=code&client_id={_options.ClientId}&redirect_uri={redirectUri}&scope={scopes}&state={state}&code_challenge={challenge}&code_challenge_method=S256";
    }

    public async Task<TwitterTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["code_verifier"] = codeVerifier
        };

        var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(_options.BaseUrl, "oauth2/token"))
        {
            Content = new FormUrlEncodedContent(form)
        };

        ApplyClientAuthentication(request);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Twitter token exchange failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Twitter token exchange failed: {ExtractErrorMessage(body)}");
        }

        var tokenResp = JsonSerializer.Deserialize<TwitterTokenResponse>(body);
        if (tokenResp == null || string.IsNullOrWhiteSpace(tokenResp.AccessToken))
        {
            throw new InvalidOperationException("Twitter returned an empty token response.");
        }

        return tokenResp;
    }

    public async Task<TwitterTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(_options.BaseUrl, "oauth2/token"))
        {
            Content = new FormUrlEncodedContent(form)
        };

        ApplyClientAuthentication(request);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Twitter token refresh failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Twitter token refresh failed: {ExtractErrorMessage(body)}");
        }

        var tokenResp = JsonSerializer.Deserialize<TwitterTokenResponse>(body);
        if (tokenResp == null || string.IsNullOrWhiteSpace(tokenResp.AccessToken))
        {
            throw new InvalidOperationException("Twitter returned an empty refresh token response.");
        }

        return tokenResp;
    }

    public async Task<TwitterUserData> FetchUserProfileAsync(string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(_options.BaseUrl, "users/me"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Twitter user profile fetch failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Twitter user profile fetch failed: {ExtractErrorMessage(body)}");
        }

        var userResp = JsonSerializer.Deserialize<TwitterUserResponse>(body);
        if (userResp?.Data == null || string.IsNullOrWhiteSpace(userResp.Data.Username))
        {
            throw new InvalidOperationException("Twitter returned an empty user profile.");
        }

        return userResp.Data;
    }

    private void ApplyClientAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            var authBytes = Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
        return baseUrl.TrimEnd('/') + "/" + relativePath.TrimStart('/');
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error_description", out var desc)) return desc.GetString() ?? body;
            if (root.TryGetProperty("error", out var err)) return err.GetString() ?? body;
            if (root.TryGetProperty("detail", out var detail)) return detail.GetString() ?? body;
            if (root.TryGetProperty("title", out var title)) return title.GetString() ?? body;
        }
        catch
        {
        }
        return body;
    }
}
