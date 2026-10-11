using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Sources.Instagram;

public class InstagramCredentialProvider : IInstagramCredentialProvider
{
    private readonly AppDbContext _db;
    private readonly IInstagramDiscoveryClient _discoveryClient;
    private readonly InstagramOptions _options;
    private readonly IConfiguration _config;
    private readonly ILogger<InstagramCredentialProvider> _logger;

    public InstagramCredentialProvider(
        AppDbContext db,
        IInstagramDiscoveryClient discoveryClient,
        IOptions<InstagramOptions> options,
        IConfiguration config,
        ILogger<InstagramCredentialProvider> logger)
    {
        _db = db;
        _discoveryClient = discoveryClient;
        _options = options.Value;
        _config = config;
        _logger = logger;
    }

    public async Task<InstagramCredentials?> GetCredentialsAsync(Guid? userId = null, CancellationToken ct = default)
    {
        var encryptionKey = _config["Auth:DbEncryptionKey"];

        var query = _db.Accounts.AsQueryable()
            .Where(a => (a.Platform == "Instagram" || a.Platform == "Meta") && a.Status == "Active");

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        var account = await query
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (account != null && !string.IsNullOrWhiteSpace(encryptionKey) && !string.IsNullOrWhiteSpace(account.CredentialsEncrypted))
        {
            try
            {
                var token = CryptoHelper.DecryptString(account.CredentialsEncrypted, encryptionKey);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var callerId = account.ExternalId;
                    if (string.IsNullOrWhiteSpace(callerId))
                    {
                        callerId = await _discoveryClient.ResolveCallerIgUserIdAsync(token, ct);
                        if (!string.IsNullOrWhiteSpace(callerId))
                        {
                            account.ExternalId = callerId;
                            account.UpdatedAt = DateTime.UtcNow;
                            await _db.SaveChangesAsync(ct);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(callerId))
                    {
                        return new InstagramCredentials(token, callerId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt or resolve credentials for Instagram account {Handle} ({Id})", account.Handle, account.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            var callerId = _options.CallerUserId;
            if (string.IsNullOrWhiteSpace(callerId))
            {
                callerId = await _discoveryClient.ResolveCallerIgUserIdAsync(_options.AccessToken, ct);
            }

            if (!string.IsNullOrWhiteSpace(callerId))
            {
                return new InstagramCredentials(_options.AccessToken, callerId);
            }
        }

        return null;
    }
}
