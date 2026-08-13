using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public record LinkedInAccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope
);

public record LinkedInUserInfoResponse(
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("given_name")] string GivenName,
    [property: JsonPropertyName("family_name")] string FamilyName
);

public sealed class LinkedInOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly LinkedInOptions _options;

    public LinkedInOAuthService(HttpClient httpClient, IOptions<LinkedInOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<LinkedInAccessTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.ClientId) || string.IsNullOrEmpty(_options.ClientSecret) || string.IsNullOrEmpty(_options.RedirectUri))
        {
            throw new InvalidOperationException("LinkedIn app credentials are not configured.");
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", _options.RedirectUri },
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret }
        });

        using var response = await _httpClient.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", form, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to exchange LinkedIn code: {response.StatusCode} - {errorContent}");
        }

        var data = await response.Content.ReadFromJsonAsync<LinkedInAccessTokenResponse>(cancellationToken: ct);
        return data ?? throw new InvalidOperationException("Empty response from access token endpoint.");
    }

    public async Task<LinkedInUserInfoResponse> FetchUserInfoAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to fetch LinkedIn user info: {response.StatusCode} - {errorContent}");
        }

        var data = await response.Content.ReadFromJsonAsync<LinkedInUserInfoResponse>(cancellationToken: ct);
        return data ?? throw new InvalidOperationException("Empty response from user info endpoint.");
    }
}
