using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyPlaceholderLinkRule : IValidateDraftRule<BlueskySegmentValidation>
{
    private static readonly Regex PlaceholderLinkTokenRegex = new(@"\[(?=[^\]\n]{0,80}(?i:link|source|youtube|docs))[^\]\n]+\](?!\()", RegexOptions.Compiled);

    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        if (!PlaceholderLinkTokenRegex.IsMatch(context.Segment.Segment))
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            "Placeholder link text detected (e.g., [source link]). Use concrete URLs or valid markdown links.");
    }
}