using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;

public sealed class BlueskyTitleLikeOpenerRule : IValidateDraftRule<BlueskySegmentValidation>
{
    private static readonly Regex TitleLikeOpenerRegex = new(@"(?i)\b(key\s+takeaways|takeaways|summary|overview|highlights)\b", RegexOptions.Compiled);

    public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
    {
        var firstNonEmptyLine = context.Segment.Segment
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;

        if (firstNonEmptyLine.Length == 0 || (!firstNonEmptyLine.EndsWith(':') && !TitleLikeOpenerRegex.IsMatch(firstNonEmptyLine)))
        {
            yield break;
        }

        yield return new ValidateDraftIssue(
            ValidateDraftSeverity.Warning,
            "Title-like opener detected. Prefer a conversational opening line for Bluesky.");
    }
}