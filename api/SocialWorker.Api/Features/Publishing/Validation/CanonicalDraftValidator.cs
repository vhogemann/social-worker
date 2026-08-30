using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure.Markdown;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.Validation;

public sealed class CanonicalDraftValidator : IDraftValidator
{
    private const int MaxCanonicalSegmentLength = 5000;

    private static readonly Regex PlaceholderLinkRegex = new(
        @"(?<!\!)\[(?i:\s*|link|url|insert\s+link|insira\s+o\s+link|placeholder)\]\([^)]+\)|(?<!\!)\[[^\]\n]+\]\((?i:\s*|#|placeholder|url|link)\)|(?<!\!)\[(?=[^\]\n]{0,80}(?i:source\s+link|insert\s+link|placeholder))[^\]\n]+\](?!\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FileSchemeRegex = new(@"file://([^\s\)]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MediaSchemeRegex = new(@"media://([^\s\)]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        if (doc.Segments.Count == 0)
        {
            return new ValidateDraftResult(
                new[]
                {
                    new ValidateDraftPostResult(
                        1,
                        0,
                        0,
                        false,
                        new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, "Draft contains no valid segments.") })
                },
                ValidateDraftOverallStatus.Failed);
        }

        var posts = new List<ValidateDraftPostResult>(doc.Segments.Count);
        bool hasErrors = false;
        bool hasWarnings = false;

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            var segDoc = doc.Segments[i];
            var segment = segDoc.RawMarkdown;
            var postNumber = i + 1;
            var issues = new List<ValidateDraftIssue>();

            var cleanedText = segDoc.PlainText;
            var imageCount = segDoc.MediaEmbeds.Count;
            var hasYouTube = segDoc.YouTubeEmbed is not null;

            if (string.IsNullOrWhiteSpace(segment) && doc.Segments.Count > 1)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Warning, $"Segment {postNumber} is empty."));
            }

            if (cleanedText.Length > MaxCanonicalSegmentLength)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Segment exceeds maximum canonical length limit ({cleanedText.Length} > {MaxCanonicalSegmentLength})."));
            }

            // Check for placeholder links
            if (PlaceholderLinkRegex.IsMatch(segment))
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "Draft contains unreplaced placeholder links. Please provide valid URLs or file references."));
            }

            // Check file references
            var fileMatches = FileSchemeRegex.Matches(segment);
            foreach (Match match in fileMatches)
            {
                var fileIdStr = match.Groups[1].Value.Trim();
                if (!Guid.TryParse(fileIdStr, out _))
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Error,
                        $"Invalid file reference format: '{fileIdStr}'. Expected a valid GUID."));
                }
            }

            // Check media references & alt text
            foreach (var mediaRef in segDoc.MediaEmbeds)
            {
                if (string.IsNullOrWhiteSpace(mediaRef.AltText))
                {
                    var asset = mediaAssets?.FirstOrDefault(m => m.Id == mediaRef.MediaId);
                    if (asset == null || string.IsNullOrWhiteSpace(asset.AltText))
                    {
                        var name = asset?.FileName ?? mediaRef.MediaId.ToString();
                        issues.Add(new ValidateDraftIssue(
                            ValidateDraftSeverity.Warning,
                            $"Image '{name}' in segment {postNumber} has missing ALT text."));
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
