using System.Collections.Generic;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyMaxImagesRule : IValidateDraftRule<BlueskySegmentValidation>
{
    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        if (context.Segment.ImageCount <= 4)
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            $"Contains {context.Segment.ImageCount} images (Bluesky allows a maximum of 4 images per post).");
    }
}