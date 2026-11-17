using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Tags;

namespace SocialWorker.Api.Features.Accounts;

public class AccountUpsertService
{
    private readonly AppDbContext _db;

    public AccountUpsertService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Account> UpsertAsync(
        Guid userId,
        string platform,
        string handle,
        string credentialsEncrypted,
        string? externalId = null,
        DateTime? tokenExpiresAt = null,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default)
    {
        Account? existing = null;
        if (!string.IsNullOrEmpty(externalId))
        {
            existing = await _db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == platform && a.ExternalId == externalId, ct);
        }

        if (existing == null)
        {
            existing = await _db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == platform && a.Handle == handle, ct);
        }

        if (existing != null)
        {
            existing.Handle = handle;
            existing.CredentialsEncrypted = credentialsEncrypted;
            if (!string.IsNullOrEmpty(externalId))
            {
                existing.ExternalId = externalId;
            }
            if (tokenExpiresAt.HasValue)
            {
                existing.TokenExpiresAt = tokenExpiresAt.Value;
            }
            if (tags != null)
            {
                existing.Tags = TagHelper.NormalizeTags(tags);
            }
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Status = "Active";
            return existing;
        }

        var normalizedTags = TagHelper.NormalizeTags(tags);
        if (normalizedTags.Count == 0)
        {
            var hasAnyForPlatform = await _db.Accounts.AnyAsync(a => a.UserId == userId && a.Platform == platform, ct);
            if (!hasAnyForPlatform)
            {
                normalizedTags = new List<string> { "default" };
            }
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = platform,
            Handle = handle,
            ExternalId = externalId,
            CredentialsEncrypted = credentialsEncrypted,
            TokenExpiresAt = tokenExpiresAt,
            Status = "Active",
            Tags = normalizedTags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        return account;
    }
}
