using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyMissingAltTextRule : IValidateDraftRule<BlueskySegmentValidation>
{
    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        var missingAltImages = new List<string>();

        foreach (var mediaRef in SharedPatterns.ExtractMediaReferences(context.Segment.Segment))
        {
            var asset = context.MediaAssets.FirstOrDefault(m => m.Id == mediaRef.MediaId);
            if (!string.IsNullOrWhiteSpace(mediaRef.AltText) || !string.IsNullOrWhiteSpace(asset?.AltText))
            {
                continue;
            }

            missingAltImages.Add(asset?.FileName ?? $"media://{mediaRef.MediaId}");
        }

        if (missingAltImages.Count == 0)
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Warning,
            $"Missing ALT text on images: {string.Join(", ", missingAltImages)}");
    }
}