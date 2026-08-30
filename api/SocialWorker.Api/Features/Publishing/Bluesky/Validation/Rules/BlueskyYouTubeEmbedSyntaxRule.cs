using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyYouTubeEmbedSyntaxRule : IValidateDraftRule<BlueskySegmentValidation>
{
    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        var segment = context.Segment.Segment;
        if (string.IsNullOrWhiteSpace(segment))
        {
            yield break;
        }

        var doc = ThreadMarkdownDocument.Parse(segment);
        var segDoc = doc.Segments.FirstOrDefault();
        if (segDoc == null)
        {
            yield break;
        }

        var hasPlainLink = segDoc.TextLinks.Any(l => ThreadSegmentDocument.IsYouTubeUrl(l.Url));
        var hasBareUrl = segDoc.BareUrls.Any(b => ThreadSegmentDocument.IsYouTubeUrl(b.Url));
        var hasRemainingInPlainText = ThreadSegmentDocument.IsYouTubeUrl(segDoc.PlainText);

        if (segDoc.AllYouTubeEmbeds.Count > 1)
        {
            yield return new ValidateDraftIssue(
                ValidateDraftSeverity.Error,
                "A post can contain at most one YouTube video embed on Bluesky.");
        }

        if (hasPlainLink || hasBareUrl || hasRemainingInPlainText)
        {
            yield return new ValidateDraftIssue(
                ValidateDraftSeverity.Error,
                "YouTube links must use embed syntax: ![Video title](https://www.youtube.com/watch?v=VIDEO_ID). Plain links or bare URLs will not embed on Bluesky.");
        }
    }
}