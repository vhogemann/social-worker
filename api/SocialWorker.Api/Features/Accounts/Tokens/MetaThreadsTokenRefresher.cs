using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Accounts.Tokens;

public class MetaThreadsTokenRefresher : ITokenRefresher
{
    private readonly MetaThreadsOAuthService _oauth;
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;
    private readonly ILogger<MetaThreadsTokenRefresher> _logger;

    public string Platform => "MetaThreads";

    public MetaThreadsTokenRefresher(
        MetaThreadsOAuthService oauth,
        AppDbContext db,
        IConfiguration config,
        ILogger<MetaThreadsTokenRefresher> logger)
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
            _logger.LogWarning("Cannot refresh Meta Threads token for account {Handle}: server encryption key missing.", account.Handle);
            return false;
        }

        string currentToken;
        try
        {
            currentToken = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credentials for Meta Threads account {Handle}", account.Handle);
            return false;
        }

        try
        {
            var refreshed = await _oauth.RefreshTokenAsync(currentToken, ct);
            var encrypted = CryptoHelper.EncryptString(refreshed.AccessToken, _encryptionKey);

            account.CredentialsEncrypted = encrypted;
            account.TokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
            account.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully refreshed Meta Threads token for account {Handle}, expires at {ExpiresAt}", account.Handle, account.TokenExpiresAt);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Meta Threads token for account {Handle}", account.Handle);
            return false;
        }
    }
}
