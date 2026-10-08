using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.PlatformVariants;

public sealed record DraftFamilyDto(
    DraftDto Canonical,
    List<DraftDto> Variants
);

public sealed record GenerateVariantsRequest(
    List<string>? Platforms = null,
    List<Guid>? AccountIds = null
);

public sealed class PlatformVariantService
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;

    public PlatformVariantService(AppDbContext db, DraftsService draftsService)
    {
        _db = db;
        _draftsService = draftsService;
    }

    public Task<DraftFamilyDto> GenerateVariantsAsync(
        Guid userId,
        Guid canonicalDraftId,
        List<string> targetPlatforms,
        CancellationToken ct = default)
    {
        return GenerateVariantsAsync(userId, canonicalDraftId, targetPlatforms, null, ct);
    }

    public async Task<DraftFamilyDto> GenerateVariantsAsync(
        Guid userId,
        Guid canonicalDraftId,
        List<string>? targetPlatforms,
        List<Guid>? targetAccountIds,
        CancellationToken ct = default)
    {
        var canonical = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == canonicalDraftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Canonical draft not found or access denied.");

        var hasAccountIds = targetAccountIds != null && targetAccountIds.Count > 0;
        var hasPlatforms = targetPlatforms != null && targetPlatforms.Count > 0;

        if (!hasAccountIds && !hasPlatforms)
        {
            throw new ArgumentException("At least one target platform or account is required.");
        }

        var variants = new List<DraftDto>();

        if (hasAccountIds)
        {
            var accounts = await _db.Accounts
                .Where(a => targetAccountIds!.Contains(a.Id) && a.UserId == userId)
                .ToListAsync(ct);

            foreach (var account in accounts)
            {
                if (account.Id == canonical.TargetAccountId)
                {
                    continue;
                }

                if (!SocialPlatformParser.TryParse(account.Platform, out var platform))
                {
                    continue;
                }

                var existingVariant = await _db.Drafts
                    .AnyAsync(d => d.CanonicalDraftId == canonicalDraftId && d.TargetAccountId == account.Id && d.Status != DraftStatus.Deleted, ct);
                if (existingVariant)
                {
                    continue;
                }

                var variant = new Draft
                {
                    Title = $"{canonical.Title} ({account.Platform} - {account.Handle})",
                    Content = canonical.Content,
                    UserId = userId,
                    TargetPlatform = platform,
                    TargetAccountId = account.Id,
                    CanonicalDraftId = canonicalDraftId,
                    Status = DraftStatus.Editing
                };
                _db.Drafts.Add(variant);
                await _db.SaveChangesAsync(ct);

                var thread = new PlatformThread
                {
                    DraftId = variant.Id,
                    Platform = account.Platform,
                    AccountId = account.Id,
                    Stage = PlatformThreadStage.Draft,
                    Content = canonical.Content
                };
                _db.PlatformThreads.Add(thread);
                await _db.SaveChangesAsync(ct);

                await _draftsService.ReconcileSegmentsAsync(variant, canonical.Content ?? "", ct);
                await _db.SaveChangesAsync(ct);

                var vDto = await _draftsService.GetDraftByIdAsync(userId, variant.Id, ct);
                variants.Add(vDto);
            }
        }
        else if (hasPlatforms)
        {
            foreach (var platformStr in targetPlatforms!)
            {
                if (!SocialPlatformParser.TryParse(platformStr, out var platform))
                {
                    continue;
                }

                if (platform == canonical.TargetPlatform)
                {
                    continue;
                }

                var platformName = platform.ToString();
                var existingVariant = await _db.Drafts
                    .AnyAsync(d => d.CanonicalDraftId == canonicalDraftId && d.TargetPlatform == platform && d.Status != DraftStatus.Deleted, ct);
                if (existingVariant)
                {
                    continue;
                }

                var variant = new Draft
                {
                    Title = $"{canonical.Title} ({platformName})",
                    Content = canonical.Content,
                    UserId = userId,
                    TargetPlatform = platform,
                    CanonicalDraftId = canonicalDraftId,
                    Status = DraftStatus.Editing
                };
                _db.Drafts.Add(variant);
                await _db.SaveChangesAsync(ct);

                var thread = new PlatformThread
                {
                    DraftId = variant.Id,
                    Platform = platformName,
                    Stage = PlatformThreadStage.Draft,
                    Content = canonical.Content
                };
                _db.PlatformThreads.Add(thread);
                await _db.SaveChangesAsync(ct);

                await _draftsService.ReconcileSegmentsAsync(variant, canonical.Content ?? "", ct);
                await _db.SaveChangesAsync(ct);

                var vDto = await _draftsService.GetDraftByIdAsync(userId, variant.Id, ct);
                variants.Add(vDto);
            }
        }

        var canonicalDto = await _draftsService.GetDraftByIdAsync(userId, canonicalDraftId, ct);
        return new DraftFamilyDto(canonicalDto, variants);
    }

    public async Task<DraftFamilyDto> GetDraftFamilyAsync(Guid userId, Guid draftId, CancellationToken ct)
    {
        var draft = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        var canonicalId = draft.CanonicalDraftId ?? draft.Id;
        var canonical = await _draftsService.GetDraftByIdAsync(userId, canonicalId, ct);

        var variantDrafts = await _db.Drafts
            .Where(d => d.CanonicalDraftId == canonicalId && d.Status != DraftStatus.Deleted)
            .OrderBy(d => d.TargetPlatform)
            .ToListAsync(ct);

        var variants = new List<DraftDto>();
        foreach (var v in variantDrafts)
        {
            var vDto = await _draftsService.GetDraftByIdAsync(userId, v.Id, ct);
            variants.Add(vDto);
        }

        return new DraftFamilyDto(canonical, variants);
    }

    public async Task<DraftDto?> GetVariantAsync(Guid userId, Guid canonicalDraftId, string platform, CancellationToken ct)
    {
        if (!SocialPlatformParser.TryParse(platform, out var targetPlatform))
        {
            return null;
        }

        var variant = await _db.Drafts
            .FirstOrDefaultAsync(d =>
                d.CanonicalDraftId == canonicalDraftId &&
                d.TargetPlatform == targetPlatform &&
                d.UserId == userId &&
                d.Status != DraftStatus.Deleted, ct);

        if (variant == null) return null;

        return await _draftsService.GetDraftByIdAsync(userId, variant.Id, ct);
    }

    public async Task<List<DraftDto>> GetVariantsForDraftAsync(Guid userId, Guid draftId, CancellationToken ct)
    {
        var draft = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        var canonicalId = draft.CanonicalDraftId ?? draft.Id;

        var variantDrafts = await _db.Drafts
            .Where(d => d.CanonicalDraftId == canonicalId && d.Status != DraftStatus.Deleted)
            .OrderBy(d => d.TargetPlatform)
            .ToListAsync(ct);

        var result = new List<DraftDto>();
        foreach (var v in variantDrafts)
        {
            var vDto = await _draftsService.GetDraftByIdAsync(userId, v.Id, ct);
            result.Add(vDto);
        }

        return result;
    }
}