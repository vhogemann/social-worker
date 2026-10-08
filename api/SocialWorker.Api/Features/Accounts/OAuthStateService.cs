using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Accounts;

public class OAuthStateService
{
    private readonly AppDbContext _db;

    public OAuthStateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> IssueAsync(
        Guid userId,
        string provider,
        string? payloadJson = null,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var lifetime = ttl ?? TimeSpan.FromMinutes(10);

        var state = new OAuthState
        {
            Nonce = nonce,
            UserId = userId,
            Provider = provider,
            PayloadJson = payloadJson,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            CreatedAt = DateTime.UtcNow
        };

        _db.OAuthStates.Add(state);
        await _db.SaveChangesAsync(ct);
        return nonce;
    }

    public async Task<OAuthState?> ConsumeAsync(
        Guid userId,
        string provider,
        string nonce,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            return null;
        }

        var state = await _db.OAuthStates
            .FirstOrDefaultAsync(s => s.Nonce == nonce && s.UserId == userId && s.Provider == provider, ct);

        if (state == null)
        {
            return null;
        }

        _db.OAuthStates.Remove(state);
        await _db.SaveChangesAsync(ct);

        if (state.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }

        return state;
    }

    public async Task<int> PruneExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.OAuthStates
            .Where(s => s.ExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        _db.OAuthStates.RemoveRange(expired);
        await _db.SaveChangesAsync(ct);
        return expired.Count;
    }
}
