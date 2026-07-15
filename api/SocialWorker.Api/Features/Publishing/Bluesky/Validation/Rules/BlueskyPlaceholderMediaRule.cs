using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyPlaceholderMediaRule : IValidateDraftRule<BlueskySegmentValidation>
{
    private static readonly Regex PlaceholderMediaTokenRegex = new(@"(?i)media://\s*(guid|\{guid\}|placeholder)", RegexOptions.Compiled);

    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        if (!PlaceholderMediaTokenRegex.IsMatch(context.Segment.Segment))
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            "Placeholder media reference detected (e.g., media://guid). Use a real media://{guid} from add_image_source or render_code_blocks.");
    }
}