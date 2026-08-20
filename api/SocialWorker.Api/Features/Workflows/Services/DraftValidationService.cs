using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Workflows.Services;

public sealed record ValidationResult(bool IsValid, string? Feedback);

public class DraftValidationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DraftValidationService> _logger;

    public DraftValidationService(
        AppDbContext db,
        ILogger<DraftValidationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateDraftAsync(
        Draft draft,
        string? platformConstraint,
        CancellationToken ct)
    {
        var loaded = await _db.Drafts
            .Include(d => d.Segments)
            .Include(d => d.Threads)
            .FirstOrDefaultAsync(d => d.Id == draft.Id, ct);

        if (loaded == null)
        {
            return new ValidationResult(false, "Draft not found.");
        }

        // 1. Basic Content Existence Check
        var hasContent = !string.IsNullOrWhiteSpace(loaded.Content) ||
                         loaded.Segments.Any(s => !string.IsNullOrWhiteSpace(s.Content)) ||
                         loaded.Threads.Any(t => !string.IsNullOrWhiteSpace(t.Content));

        if (!hasContent)
        {
            _logger.LogWarning("Draft {DraftId} failed validation: No content or segments generated.", draft.Id);
            return new ValidationResult(false, "No content or segments generated.");
        }

        // 2. Platform Constraint Check (Character limits)
        if (string.Equals(platformConstraint, "Bluesky", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platformConstraint, "Twitter", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var segment in loaded.Segments)
            {
                if (segment.Content != null && segment.Content.Length > 300)
                {
                    _logger.LogWarning("Draft {DraftId} failed validation: Segment exceeds character limit ({Length} > 300).", draft.Id, segment.Content.Length);
                    return new ValidationResult(false, $"Segment exceeds {platformConstraint} character limit.");
                }
            }
        }

        return new ValidationResult(true, null);
    }
}
