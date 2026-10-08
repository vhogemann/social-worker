using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Publishing.Validation;

public class PublishValidationService
{
    private readonly IDraftValidatorResolver _resolver;
    private readonly AppDbContext _db;

    public PublishValidationService(IDraftValidatorResolver resolver, AppDbContext db)
    {
        _resolver = resolver;
        _db = db;
    }

    public async Task<string?> ValidateForPublishAsync(PlatformThread thread, CancellationToken ct = default)
    {
        if (!SocialPlatformParser.TryParse(thread.Platform, out var platform))
        {
            return $"Invalid platform: {thread.Platform}";
        }

        var mediaAssets = await _db.MediaAssets
            .Where(m => m.DraftId == thread.DraftId)
            .ToListAsync(ct);

        var validator = _resolver.Resolve(platform);
        var report = validator.Validate(thread.Content ?? string.Empty, mediaAssets);

        var blockingError = report.Posts
            .SelectMany(p => p.Issues)
            .FirstOrDefault(i => i.Severity == ValidateDraftSeverity.Error);

        return blockingError?.Message;
    }
}
