using System.Collections.Generic;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyUnsupportedMarkdownRule : IValidateDraftRule<BlueskySegmentValidation>
{
    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        if (!context.Segment.HasUnsupportedMarkdown)
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            "Unsupported markdown styling detected for Bluesky (bold/italic/heading markers). Use plain text formatting.");
    }
}