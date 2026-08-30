using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Infrastructure.Auth;

public sealed record GeneratedApiKeyResult(
    Guid Id,
    string Name,
    string PlaintextKey,
    string KeyPrefix,
    string Scopes,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Scopes,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    bool IsRevoked);

public sealed class ApiKeyService
{
    public const string KeyPrefixTag = "sw_live_";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public ApiKeyService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public static string ComputeSha256Hash(string rawKey)
    {
        var bytes = Encoding.UTF8.GetBytes(rawKey.Trim());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<GeneratedApiKeyResult> CreateApiKeyAsync(
        Guid userId,
        string name,
        string? scopes = null,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        var rawKey = $"{KeyPrefixTag}{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        var prefix = rawKey.Substring(0, Math.Min(16, rawKey.Length));
        var hash = ComputeSha256Hash(rawKey);

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(name) ? "API Key" : name.Trim(),
            KeyPrefix = prefix,
            KeyHash = hash,
            Scopes = string.IsNullOrWhiteSpace(scopes) ? "*" : scopes.Trim(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return new GeneratedApiKeyResult(
            apiKey.Id,
            apiKey.Name,
            rawKey,
            apiKey.KeyPrefix,
            apiKey.Scopes,
            apiKey.CreatedAt,
            apiKey.ExpiresAt);
    }

    public async Task<ApiKey?> ValidateApiKeyAsync(string? rawKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        var trimmedKey = rawKey.Trim();
        if (trimmedKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            trimmedKey = trimmedKey.Substring(7).Trim();
        }

        if (!trimmedKey.StartsWith(KeyPrefixTag, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hash = ComputeSha256Hash(trimmedKey);
        var cacheKey = $"apikey:hash:{hash}";

        if (_cache.TryGetValue(cacheKey, out ApiKey? cachedKey) && cachedKey != null)
        {
            if (cachedKey.IsRevoked || (cachedKey.ExpiresAt.HasValue && cachedKey.ExpiresAt.Value <= DateTime.UtcNow))
            {
                _cache.Remove(cacheKey);
                return null;
            }
            return cachedKey;
        }

        var key = await _db.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash, ct);

        if (key == null || key.IsRevoked || (key.ExpiresAt.HasValue && key.ExpiresAt.Value <= DateTime.UtcNow))
        {
            return null;
        }

        key.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _cache.Set(cacheKey, key, TimeSpan.FromMinutes(5));
        return key;
    }

    public async Task<bool> RevokeApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken ct = default)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId, ct);
        if (key == null)
        {
            return false;
        }

        key.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        var cacheKey = $"apikey:hash:{key.KeyHash}";
        _cache.Remove(cacheKey);
        return true;
    }

    public async Task<List<ApiKeyDto>> ListApiKeysAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.Scopes,
                k.CreatedAt,
                k.LastUsedAt,
                k.ExpiresAt,
                k.IsRevoked))
            .ToListAsync(ct);
    }
}
