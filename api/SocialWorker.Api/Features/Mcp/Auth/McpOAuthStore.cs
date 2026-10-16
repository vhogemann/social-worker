using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SocialWorker.Api.Features.Mcp.Auth;

public sealed class McpOAuthStore
{
    private readonly ConcurrentDictionary<string, OAuthClientInfo> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AuthorizationCodeEntry> _authCodes = new(StringComparer.Ordinal);

    public McpOAuthStore()
    {
        var defaultClients = new[]
        {
            new OAuthClientInfo("claude", "Claude", Array.Empty<string>(), DateTime.UtcNow),
            new OAuthClientInfo("claude-desktop", "Claude Desktop", Array.Empty<string>(), DateTime.UtcNow),
            new OAuthClientInfo("claude-code", "Claude Code", Array.Empty<string>(), DateTime.UtcNow),
            new OAuthClientInfo("cursor", "Cursor", Array.Empty<string>(), DateTime.UtcNow)
        };

        foreach (var client in defaultClients)
        {
            _clients[client.ClientId] = client;
        }
    }

    public OAuthClientInfo RegisterClient(string? clientName, string[]? redirectUris)
    {
        var clientId = $"client_{Guid.NewGuid():N}";
        var name = string.IsNullOrWhiteSpace(clientName) ? "MCP Client" : clientName.Trim();
        var uris = redirectUris ?? Array.Empty<string>();

        var client = new OAuthClientInfo(clientId, name, uris, DateTime.UtcNow);
        _clients[clientId] = client;
        return client;
    }

    public OAuthClientInfo? GetClient(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        return _clients.TryGetValue(clientId, out var client) ? client : null;
    }

    public string CreateAuthorizationCode(
        string clientId,
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        Guid userId,
        string scope)
    {
        var rawCode = $"code_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
        var entry = new AuthorizationCodeEntry(
            Code: rawCode,
            ClientId: clientId,
            RedirectUri: redirectUri,
            CodeChallenge: codeChallenge,
            CodeChallengeMethod: string.IsNullOrWhiteSpace(codeChallengeMethod) ? "S256" : codeChallengeMethod,
            UserId: userId,
            Scope: string.IsNullOrWhiteSpace(scope) ? "mcp:tools" : scope,
            ExpiresAt: DateTime.UtcNow.AddMinutes(5));

        _authCodes[rawCode] = entry;
        return rawCode;
    }

    public AuthorizationCodeEntry? ConsumeAuthorizationCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        if (_authCodes.TryRemove(code, out var entry))
        {
            if (entry.ExpiresAt >= DateTime.UtcNow)
            {
                return entry;
            }
        }

        return null;
    }

    public static bool VerifyPkce(string codeVerifier, string codeChallenge, string? method)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier) || string.IsNullOrWhiteSpace(codeChallenge))
        {
            return false;
        }

        if (string.Equals(method, "plain", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(codeVerifier, codeChallenge, StringComparison.Ordinal);
        }

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier.Trim()));
        var base64Url = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return string.Equals(base64Url, codeChallenge.Trim(), StringComparison.Ordinal);
    }
}
