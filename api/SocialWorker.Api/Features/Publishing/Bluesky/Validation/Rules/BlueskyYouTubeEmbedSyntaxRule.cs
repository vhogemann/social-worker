using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyYouTubeEmbedSyntaxRule : IValidateDraftRule<BlueskySegmentValidation>
{
    private const string YouTubeUrlPattern =
        @"https?://(?:www\.)?(?:youtube\.com/(?:(?:watch\?[^)\s]+)|(?:shorts/[^)\s]+)|(?:embed/[^)\s]+)|(?:live/[^)\s]+))|youtu\.be/[^)\s]+)";

    private static readonly Regex YouTubeMarkdownLinkWithoutEmbedRegex =
        new($@"(?<!!)\[[^\]\n]+\]\(({YouTubeUrlPattern})\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BareYouTubeUrlRegex =
        new($@"(?<!\()\b{YouTubeUrlPattern}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        var segment = context.Segment.Segment;
        if (string.IsNullOrWhiteSpace(segment))
        {
            yield break;
        }

        if (!YouTubeMarkdownLinkWithoutEmbedRegex.IsMatch(segment) && !BareYouTubeUrlRegex.IsMatch(segment))
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Error,
            "YouTube links must use embed syntax: ![Video title](https://www.youtube.com/watch?v=VIDEO_ID). Plain links or bare URLs will not embed on Bluesky.");
    }
}