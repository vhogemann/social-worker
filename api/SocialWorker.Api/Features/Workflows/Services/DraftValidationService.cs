using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Workflows.Services;

public sealed record ValidationResult(bool IsValid, string? Feedback);

public class DraftValidationService
{
    private readonly AppDbContext _db;
    private readonly IDraftValidatorResolver _validatorResolver;
    private readonly ILogger<DraftValidationService> _logger;

    public DraftValidationService(
        AppDbContext db,
        IDraftValidatorResolver validatorResolver,
        ILogger<DraftValidationService> logger)
    {
        _db = db;
        _validatorResolver = validatorResolver;
        _logger = logger;
    }

    public virtual async Task<ValidationResult> ValidateDraftAsync(
        Draft draft,
        string? platformConstraint,
        CancellationToken ct)
    {
        var loaded = await _db.Drafts
            .Include(d => d.Segments)
            .Include(d => d.Threads)
            .Include(d => d.MediaAssets)
            .FirstOrDefaultAsync(d => d.Id == draft.Id, ct);

        if (loaded == null)
        {
            return new ValidationResult(false, "Draft not found.");
        }

        var content = loaded.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            var segmentContents = loaded.Segments
                .OrderBy(s => s.Position)
                .Select(s => s.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c));
            content = string.Join("\n\n---\n\n", segmentContents);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Draft {DraftId} failed validation: No content or segments generated.", draft.Id);
            return new ValidationResult(false, "No content or segments generated.");
        }

        SocialPlatform? targetPlatform = null;
        if (!string.IsNullOrWhiteSpace(platformConstraint) &&
            SocialPlatformParser.TryParse(platformConstraint, out var explicitPlatform))
        {
            targetPlatform = explicitPlatform;
        }
        else
        {
            targetPlatform = loaded.TargetPlatform;
        }

        var validator = _validatorResolver.Resolve(targetPlatform);
        var report = validator.Validate(content, loaded.MediaAssets?.ToList() ?? new List<MediaAsset>());

        if (report.HasBlockingErrors)
        {
            var errors = report.Posts
                .SelectMany(p => p.Issues)
                .Where(i => i.Severity == ValidateDraftSeverity.Error)
                .Select(i => i.Message)
                .Distinct();

            var feedback = string.Join(" ", errors);
            _logger.LogWarning("Draft {DraftId} failed validation for platform {Platform}: {Feedback}",
                draft.Id, targetPlatform?.ToString() ?? "Canonical", feedback);

            return new ValidationResult(false, feedback);
        }

        return new ValidationResult(true, null);
    }
}
