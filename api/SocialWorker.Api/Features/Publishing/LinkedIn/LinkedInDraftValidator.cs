using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public sealed class LinkedInDraftValidator : IDraftValidator
{
    private const int MaxCharacters = 3000;
    private static readonly Regex MarkdownStylingRegex = new(@"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);
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

        var rawSegments = DraftSegmentService.SplitMarkdownIntoSegments(content);
        var nonEmpties = rawSegments.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        var posts = new List<ValidateDraftPostResult>();
        bool hasErrors = false;
        bool hasWarnings = false;

        for (int i = 0; i < rawSegments.Count; i++)
        {
            var segment = rawSegments[i];
            var postNumber = i + 1;
            var issues = new List<ValidateDraftIssue>();

            var cleanedText = SharedPatterns.StripMediaMarkdown(segment).Trim();
            var mediaRefs = SharedPatterns.ExtractMediaReferences(segment);
            var imageCount = mediaRefs.Count;
            var hasYouTube = segment.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                             segment.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
            var hasUrlLink = segment.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                             segment.Contains("https://", StringComparison.OrdinalIgnoreCase);

            if (nonEmpties.Count > 1)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "LinkedIn does not support multi-post threads. Exactly one post segment is allowed."));
            }

            if (cleanedText.Length > MaxCharacters)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Post exceeds the {MaxCharacters}-character LinkedIn limit by {cleanedText.Length - MaxCharacters} characters."));
            }

            if (imageCount > 1)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "LinkedIn posts currently support at most 1 image attachment."));
            }

            if (imageCount > 0 && (hasYouTube || hasUrlLink))
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "Cannot mix image attachments and URL link shares in a single LinkedIn post."));
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
                    "Markdown styling (bold, italic, headings) detected. LinkedIn renders formatting markers as plain text unless normalized."));
            }

            foreach (var mediaRef in mediaRefs)
            {
                if (string.IsNullOrWhiteSpace(mediaRef.AltText))
                {
                    var asset = mediaAssets?.FirstOrDefault(m => m.Id == mediaRef.MediaId);
                    if (asset == null || string.IsNullOrWhiteSpace(asset.AltText))
                    {
                        var name = asset?.FileName ?? mediaRef.MediaId.ToString();
                        issues.Add(new ValidateDraftIssue(
                            ValidateDraftSeverity.Warning,
                            $"Missing ALT text on image: {name}."));
                    }
                }
            }

            hasErrors |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Error);
            hasWarnings |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Warning);

            posts.Add(new ValidateDraftPostResult(
                postNumber,
                cleanedText.Length,
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
