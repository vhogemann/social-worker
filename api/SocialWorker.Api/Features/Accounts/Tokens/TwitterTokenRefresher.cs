using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Accounts.Tokens;

public class TwitterTokenRefresher : ITokenRefresher
{
    private readonly TwitterOAuthService _oauth;
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;
    private readonly ILogger<TwitterTokenRefresher> _logger;

    public string Platform => "Twitter";

    public TwitterTokenRefresher(
        TwitterOAuthService oauth,
        AppDbContext db,
        IConfiguration config,
        ILogger<TwitterTokenRefresher> logger)
    {
        _oauth = oauth;
        _db = db;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
        _logger = logger;
    }

    public async Task<bool> RefreshAccountTokenAsync(Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            _logger.LogWarning("Cannot refresh Twitter token for account {Handle}: server encryption key missing.", account.Handle);
            return false;
        }

        string decrypted;
        try
        {
            decrypted = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credentials for Twitter account {Handle}", account.Handle);
            account.Status = "Invalid";
            account.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        TwitterAccountCredentials? credentials = null;
        try
        {
            credentials = JsonSerializer.Deserialize<TwitterAccountCredentials>(decrypted);
        }
        catch
        {
        }

        var refreshToken = credentials?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Twitter account {Handle} does not have a refresh token.", account.Handle);
            account.Status = "Invalid";
            account.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        try
        {
            var refreshed = await _oauth.RefreshTokenAsync(refreshToken, ct);
            var updatedCredentials = new TwitterAccountCredentials(
                AccessToken: refreshed.AccessToken,
                RefreshToken: refreshed.RefreshToken ?? refreshToken,
                TokenType: refreshed.TokenType ?? "bearer",
                Scope: refreshed.Scope ?? credentials?.Scope);

            var updatedJson = JsonSerializer.Serialize(updatedCredentials);
            account.CredentialsEncrypted = CryptoHelper.EncryptString(updatedJson, _encryptionKey);
            account.TokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
            account.Status = "Active";
            account.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully refreshed and rotated Twitter token for account {Handle}, expires at {ExpiresAt}", account.Handle, account.TokenExpiresAt);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Twitter token for account {Handle}. Marking account as Invalid.", account.Handle);
            account.Status = "Invalid";
            account.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }
    }
}
