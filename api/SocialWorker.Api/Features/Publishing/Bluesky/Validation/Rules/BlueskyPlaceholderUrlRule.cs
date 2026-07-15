using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyPlaceholderUrlRule : IValidateDraftRule<BlueskySegmentValidation>
{
    private static readonly Regex PlaceholderUrlRegex = new(@"(?i)https?://(www\.)?example\.com\b", RegexOptions.Compiled);

    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        if (!PlaceholderUrlRegex.IsMatch(context.Segment.Segment))
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            "Placeholder URL detected (e.g., example.com). Use a concrete source URL.");
    }
}