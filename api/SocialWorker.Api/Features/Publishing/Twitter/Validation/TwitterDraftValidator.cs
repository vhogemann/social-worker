using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.Twitter.Validation;

public sealed class TwitterDraftValidator : IDraftValidator
{
    public const int MaxCharacters = 280;

    private static readonly Regex MarkdownStylingRegex = new(
        @"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownLinkRegex = new(
        @"(?<!\!)\[(?<label>[^\]\n]+)\]\((?<url>[^)\s]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex PlaceholderLinkRegex = new(
        @"(?<!\!)\[(?i:\s*|link|url|insert\s+link|insira\s+o\s+link|placeholder)\]\([^)]+\)|(?<!\!)\[[^\]\n]+\]\((?i:\s*|#|placeholder|url|link)\)|(?<!\!)\[(?=[^\]\n]{0,80}(?i:source\s+link|insert\s+link|placeholder))[^\]\n]+\](?!\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ValidateDraftResult Validate(string content, IReadOnlyList<MediaAsset> mediaAssets)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ValidateDraftResult(
                new[]
                {
                    new ValidateDraftPostResult(
                        1,
                        0,
                        0,
                        false,
                        new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, "Draft content cannot be empty.") })
                },
                ValidateDraftOverallStatus.Failed);
        }

        var doc = ThreadMarkdownDocument.Parse(content);
        var posts = new List<ValidateDraftPostResult>();
        bool hasErrors = false;
        bool hasWarnings = false;

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            var segDoc = doc.Segments[i];
            var segment = segDoc.RawMarkdown;
            var issues = new List<ValidateDraftIssue>();

            var cleanedText = segDoc.PlainText;
            var weightedLength = TwitterTextWeightCalculator.CalculateWeight(cleanedText);
            var mediaRefs = segDoc.MediaEmbeds;
            var imageCount = mediaRefs.Count;
            var hasYouTube = segDoc.YouTubeEmbed is not null || segDoc.HasYouTube;

            if (weightedLength > MaxCharacters)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Post exceeds the {MaxCharacters} weighted-character limit for Twitter by {weightedLength - MaxCharacters} units."));
            }

            if (imageCount > 0)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "Media attachments are not currently supported for Twitter in v1. Please remove images/media before publishing."));
            }

            if (hasYouTube)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Warning,
                    "Twitter does not support inline video players for YouTube embeds; URL will be published as text."));
            }

            if (PlaceholderLinkRegex.IsMatch(segment))
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "Draft contains unreplaced placeholder links."));
            }

            if (MarkdownStylingRegex.IsMatch(segment))
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Warning,
                    "Markdown styling (bold, italic, headings) detected. Twitter displays plain text; formatting symbols will be visible to readers."));
            }

            foreach (Match match in MarkdownLinkRegex.Matches(segment))
            {
                var label = match.Groups["label"].Value.Trim();
                var url = match.Groups["url"].Value.Trim();
                if (!string.Equals(label, url, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Warning,
                        $"Markdown link '[{label}]({url})' detected. Twitter displays plain text; use direct URLs so links remain clickable."));
                }
            }

            hasErrors |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Error);
            hasWarnings |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Warning);

            posts.Add(new ValidateDraftPostResult(
                i + 1,
                weightedLength,
                imageCount,
                hasYouTube,
                issues));
        }

        var overallStatus = hasErrors
            ? ValidateDraftOverallStatus.Failed
            : hasWarnings
                ? ValidateDraftOverallStatus.Warnings
                : ValidateDraftOverallStatus.Valid;

        return new ValidateDraftResult(posts, overallStatus);
    }
}
