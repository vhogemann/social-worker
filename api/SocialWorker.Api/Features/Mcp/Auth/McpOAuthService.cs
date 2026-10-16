using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Auth;

namespace SocialWorker.Api.Features.Mcp.Auth;

public sealed class McpOAuthService
{
    private readonly AppDbContext _db;
    private readonly McpOAuthStore _store;
    private readonly AuthOptions _authOptions;

    public McpOAuthService(
        AppDbContext db,
        McpOAuthStore store,
        IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _store = store;
        _authOptions = authOptions.Value;
    }

    public async Task<AppUser?> GetDefaultOrAdminUserAsync(Guid? userId = null, CancellationToken ct = default)
    {
        if (userId.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value && u.IsActive, ct);
            if (user != null) return user;
        }

        return await _db.Users.FirstOrDefaultAsync(u => u.Username == "admin" && u.IsActive, ct)
               ?? await _db.Users.FirstOrDefaultAsync(u => u.IsActive, ct);
    }

    public async Task<(OAuthTokenResponse? Response, string? Error, string? ErrorDescription)> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        string clientId,
        string codeVerifier,
        string baseUrl,
        CancellationToken ct = default)
    {
        var entry = _store.ConsumeAuthorizationCode(code);
        if (entry == null)
        {
            return (null, "invalid_grant", "Authorization code is invalid, expired, or already used.");
        }

        if (!string.Equals(entry.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "invalid_grant", "Client ID does not match the authorization request.");
        }

        if (!string.Equals(entry.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            return (null, "invalid_grant", "Redirect URI does not match the authorization request.");
        }

        if (!McpOAuthStore.VerifyPkce(codeVerifier, entry.CodeChallenge, entry.CodeChallengeMethod))
        {
            return (null, "invalid_grant", "PKCE code verifier verification failed.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId && u.IsActive, ct);
        if (user == null)
        {
            return (null, "invalid_grant", "Authorized user not found or inactive.");
        }

        var accessToken = CreateMcpAccessToken(user, baseUrl, entry.Scope, out var expiresIn);
        var refreshTokenStr = CreateSecureRandomToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenStr,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var response = new OAuthTokenResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: expiresIn,
            RefreshToken: refreshTokenStr,
            Scope: entry.Scope);

        return (response, null, null);
    }

    public async Task<(OAuthTokenResponse? Response, string? Error, string? ErrorDescription)> RefreshTokenAsync(
        string refreshTokenStr,
        string baseUrl,
        CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshTokenStr, ct);

        if (token == null || !token.User.IsActive || token.ExpiresAt < DateTime.UtcNow)
        {
            if (token != null)
            {
                _db.RefreshTokens.Remove(token);
                await _db.SaveChangesAsync(ct);
            }
            return (null, "invalid_grant", "Refresh token is invalid, expired, or user is inactive.");
        }

        var accessToken = CreateMcpAccessToken(token.User, baseUrl, "mcp:tools", out var expiresIn);

        token.LastUsedAt = DateTime.UtcNow;
        token.ExpiresAt = DateTime.UtcNow.AddDays(30);
        await _db.SaveChangesAsync(ct);

        var response = new OAuthTokenResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: expiresIn,
            RefreshToken: token.Token,
            Scope: "mcp:tools");

        return (response, null, null);
    }

    private string CreateMcpAccessToken(AppUser user, string baseUrl, string scope, out int expiresInSeconds)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_authOptions.JwtSecret);
        expiresInSeconds = 3600;
        var expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("scopes", scope),
                new Claim("auth_method", "mcp_oauth")
            }),
            Issuer = baseUrl,
            Audience = $"{baseUrl}/api/mcp",
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string CreateSecureRandomToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
