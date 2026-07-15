using System.Collections.Generic;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyNoMixedImagesAndYouTubeRule : IValidateDraftRule<BlueskySegmentValidation>
{
    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        if (context.Segment.ImageCount == 0 || !context.Segment.HasYouTube)
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            "Cannot mix images and YouTube embeds in a single post on Bluesky.");
    }
}