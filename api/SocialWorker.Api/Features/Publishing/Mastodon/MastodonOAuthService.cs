using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.Mastodon;

public sealed class MastodonOAuthService
{
    private readonly HttpClient _http;
    private readonly MastodonOptions _options;

    public MastodonOAuthService(HttpClient http, IOptions<MastodonOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public static string NormalizeInstanceUrl(string instanceInput)
    {
        if (string.IsNullOrWhiteSpace(instanceInput))
        {
            throw new ArgumentException("Mastodon instance domain is required.", nameof(instanceInput));
        }

        var trimmed = instanceInput.Trim().TrimEnd('/');
        if (trimmed.Contains('@'))
        {
            var atIndex = trimmed.LastIndexOf('@');
            trimmed = trimmed[(atIndex + 1)..].Trim();
        }

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Invalid Mastodon instance URL. Provide a valid HTTPS domain (e.g. mastodon.social).", nameof(instanceInput));
        }

        if (IsBlockedHost(uri.Host))
        {
            throw new ArgumentException("Mastodon instance host is not allowed.", nameof(instanceInput));
        }

        return uri.IsDefaultPort
            ? $"https://{uri.Host.ToLowerInvariant()}"
            : $"https://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }

    public static string FormatHandle(string username, string instanceUrl)
    {
        var cleanUser = username.Trim().TrimStart('@');
        var host = new Uri(instanceUrl).Host.ToLowerInvariant();
        if (cleanUser.Contains('@'))
        {
            return $"@{cleanUser}";
        }

        return $"@{cleanUser}@{host}";
    }

    public static string? TryExtractInstanceFromHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var trimmed = handle.Trim();
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var parts = trimmed.TrimStart('@').Split('@', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : null;
    }

    public async Task<MastodonAppRegistrationResponse> RegisterAppAsync(
        string instanceUrl,
        string redirectUri,
        CancellationToken ct = default)
    {
        var appName = string.IsNullOrWhiteSpace(_options.AppName) ? "social-worker" : _options.AppName;
        var scopes = string.IsNullOrWhiteSpace(_options.Scopes) ? "read write" : _options.Scopes;

        var req = new MastodonAppRegistrationRequest(appName, redirectUri, scopes);
        using var res = await _http.PostAsJsonAsync($"{instanceUrl}/api/v1/apps", req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to register Mastodon OAuth app on {instanceUrl} ({(int)res.StatusCode}): {body}");
        }

        var data = await res.Content.ReadFromJsonAsync<MastodonAppRegistrationResponse>(cancellationToken: ct);
        if (data is null || string.IsNullOrWhiteSpace(data.ClientId) || string.IsNullOrWhiteSpace(data.ClientSecret))
        {
            throw new InvalidOperationException($"Mastodon instance {instanceUrl} returned an invalid app registration response.");
        }

        return data;
    }

    public string GetAuthorizationUrl(string instanceUrl, string clientId, string redirectUri, string state)
    {
        var scopes = string.IsNullOrWhiteSpace(_options.Scopes) ? "read write" : _options.Scopes;
        var query = string.Join("&", new[]
        {
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"scope={Uri.EscapeDataString(scopes)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            $"state={Uri.EscapeDataString(state)}"
        });
        return $"{instanceUrl}/oauth/authorize?{query}";
    }

    public async Task<MastodonTokenResponse> ExchangeCodeAsync(
        string instanceUrl,
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken ct = default)
    {
        var scopes = string.IsNullOrWhiteSpace(_options.Scopes) ? "read write" : _options.Scopes;
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["scope"] = scopes
        };

        using var content = new FormUrlEncodedContent(form);
        using var res = await _http.PostAsync($"{instanceUrl}/oauth/token", content, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to exchange Mastodon OAuth code ({(int)res.StatusCode}): {body}");
        }

        var data = await res.Content.ReadFromJsonAsync<MastodonTokenResponse>(cancellationToken: ct);
        if (data is null || string.IsNullOrWhiteSpace(data.AccessToken))
        {
            throw new InvalidOperationException("Mastodon returned an empty access token.");
        }

        return data;
    }

    public async Task<MastodonAccountProfile> VerifyCredentialsAsync(
        string instanceUrl,
        string accessToken,
        CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{instanceUrl}/api/v1/accounts/verify_credentials");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _http.SendAsync(message, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to verify Mastodon credentials ({(int)res.StatusCode}): {body}");
        }

        var profile = await res.Content.ReadFromJsonAsync<MastodonAccountProfile>(cancellationToken: ct);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Username))
        {
            throw new InvalidOperationException("Mastodon verify_credentials returned an invalid profile.");
        }

        return profile;
    }

    private static bool IsBlockedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 10 ||
                bytes[0] == 127 ||
                bytes[0] == 0 ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168))
            {
                return true;
            }
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            {
                return true;
            }
        }

        return false;
    }
}
