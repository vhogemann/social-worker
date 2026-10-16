using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Mcp.Auth;
using SocialWorker.Api.Infrastructure.Auth;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Mcp;

public sealed class McpOAuthTests : SqliteTestBase
{
    private readonly McpOAuthStore _store = new();

    private McpOAuthService CreateOAuthService(AppDbContext db, string jwtSecret = "super_secret_jwt_key_that_is_long_enough_for_sha256_security")
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new AuthOptions
        {
            JwtSecret = jwtSecret,
            AccessTokenLifetimeMinutes = 60,
            RefreshTokenLifetimeDays = 30
        });

        return new McpOAuthService(db, _store, opts);
    }

    private static (string CodeVerifier, string CodeChallenge) GeneratePkce()
    {
        var verifier = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (verifier, challenge);
    }

    [Fact]
    public void McpOAuthStore_HasDefaultClientsConfigured()
    {
        Assert.NotNull(_store.GetClient("claude"));
        Assert.NotNull(_store.GetClient("claude-desktop"));
        Assert.NotNull(_store.GetClient("claude-code"));
        Assert.NotNull(_store.GetClient("cursor"));
    }

    [Fact]
    public void McpOAuthStore_RegisterClient_StoresAndRetrievesClient()
    {
        var client = _store.RegisterClient("Test Claude Integration", new[] { "http://localhost:54321/callback" });

        Assert.NotNull(client);
        Assert.StartsWith("client_", client.ClientId);
        Assert.Equal("Test Claude Integration", client.ClientName);
        Assert.Single(client.RedirectUris);
        Assert.Equal("http://localhost:54321/callback", client.RedirectUris[0]);

        var retrieved = _store.GetClient(client.ClientId);
        Assert.NotNull(retrieved);
        Assert.Equal(client.ClientId, retrieved.ClientId);
    }

    [Fact]
    public void VerifyPkce_S256_ValidVerifier_ReturnsTrue()
    {
        var (verifier, challenge) = GeneratePkce();
        var isValid = McpOAuthStore.VerifyPkce(verifier, challenge, "S256");
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPkce_S256_InvalidVerifier_ReturnsFalse()
    {
        var (_, challenge) = GeneratePkce();
        var isValid = McpOAuthStore.VerifyPkce("wrong_verifier", challenge, "S256");
        Assert.False(isValid);
    }

    [Fact]
    public void AuthorizationCode_OneTimeUse_SecondConsumeReturnsNull()
    {
        var user = CreateSeedUser();
        var (verifier, challenge) = GeneratePkce();

        var code = _store.CreateAuthorizationCode(
            clientId: "claude-desktop",
            redirectUri: "http://localhost:1234/cb",
            codeChallenge: challenge,
            codeChallengeMethod: "S256",
            userId: user.Id,
            scope: "mcp:tools");

        var firstEntry = _store.ConsumeAuthorizationCode(code);
        Assert.NotNull(firstEntry);
        Assert.Equal("claude-desktop", firstEntry.ClientId);
        Assert.Equal(user.Id, firstEntry.UserId);

        var secondEntry = _store.ConsumeAuthorizationCode(code);
        Assert.Null(secondEntry);
    }

    [Fact]
    public async Task ExchangeAuthorizationCode_ValidRequest_IssuesJwtAndRefreshToken()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db, "admin", "admin@socialworker.local");
        var oauthService = CreateOAuthService(db);
        var (verifier, challenge) = GeneratePkce();

        var code = _store.CreateAuthorizationCode(
            clientId: "claude-desktop",
            redirectUri: "http://localhost:1234/callback",
            codeChallenge: challenge,
            codeChallengeMethod: "S256",
            userId: user.Id,
            scope: "mcp:tools");

        var (res, err, errDesc) = await oauthService.ExchangeAuthorizationCodeAsync(
            code: code,
            redirectUri: "http://localhost:1234/callback",
            clientId: "claude-desktop",
            codeVerifier: verifier,
            baseUrl: "http://localhost:8100",
            ct: CancellationToken.None);

        Assert.Null(err);
        Assert.Null(errDesc);
        Assert.NotNull(res);
        Assert.Equal("Bearer", res.TokenType);
        Assert.Equal(3600, res.ExpiresIn);
        Assert.NotNull(res.AccessToken);
        Assert.NotNull(res.RefreshToken);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(res.AccessToken);
        Assert.Equal("http://localhost:8100", jwt.Issuer);
        Assert.Contains("http://localhost:8100/api/mcp", jwt.Audiences);

        var storedRefreshToken = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == res.RefreshToken);
        Assert.NotNull(storedRefreshToken);
        Assert.Equal(user.Id, storedRefreshToken.UserId);
    }

    [Fact]
    public async Task ExchangeAuthorizationCode_InvalidVerifier_Fails()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var oauthService = CreateOAuthService(db);
        var (_, challenge) = GeneratePkce();

        var code = _store.CreateAuthorizationCode(
            clientId: "claude-desktop",
            redirectUri: "http://localhost:1234/callback",
            codeChallenge: challenge,
            codeChallengeMethod: "S256",
            userId: user.Id,
            scope: "mcp:tools");

        var (res, err, _) = await oauthService.ExchangeAuthorizationCodeAsync(
            code: code,
            redirectUri: "http://localhost:1234/callback",
            clientId: "claude-desktop",
            codeVerifier: "invalid_verifier",
            baseUrl: "http://localhost:8100",
            ct: CancellationToken.None);

        Assert.Null(res);
        Assert.Equal("invalid_grant", err);
    }

    [Fact]
    public async Task ExchangeAuthorizationCode_MismatchedRedirectUri_Fails()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var oauthService = CreateOAuthService(db);
        var (verifier, challenge) = GeneratePkce();

        var code = _store.CreateAuthorizationCode(
            clientId: "claude-desktop",
            redirectUri: "http://localhost:1234/callback",
            codeChallenge: challenge,
            codeChallengeMethod: "S256",
            userId: user.Id,
            scope: "mcp:tools");

        var (res, err, _) = await oauthService.ExchangeAuthorizationCodeAsync(
            code: code,
            redirectUri: "http://localhost:9999/wrong-callback",
            clientId: "claude-desktop",
            codeVerifier: verifier,
            baseUrl: "http://localhost:8100",
            ct: CancellationToken.None);

        Assert.Null(res);
        Assert.Equal("invalid_grant", err);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_ReturnsNewAccessToken()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var oauthService = CreateOAuthService(db);

        var tokenStr = "test_refresh_token_hex_1234567890";
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = tokenStr,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var (res, err, _) = await oauthService.RefreshTokenAsync(
            refreshTokenStr: tokenStr,
            baseUrl: "http://localhost:8100",
            ct: CancellationToken.None);

        Assert.Null(err);
        Assert.NotNull(res);
        Assert.NotNull(res.AccessToken);
        Assert.Equal(3600, res.ExpiresIn);
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_Fails()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var oauthService = CreateOAuthService(db);

        var tokenStr = "expired_refresh_token_hex_0000";
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = tokenStr,
            CreatedAt = DateTime.UtcNow.AddDays(-40),
            LastUsedAt = DateTime.UtcNow.AddDays(-40),
            ExpiresAt = DateTime.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var (res, err, _) = await oauthService.RefreshTokenAsync(
            refreshTokenStr: tokenStr,
            baseUrl: "http://localhost:8100",
            ct: CancellationToken.None);

        Assert.Null(res);
        Assert.Equal("invalid_grant", err);
    }
}
