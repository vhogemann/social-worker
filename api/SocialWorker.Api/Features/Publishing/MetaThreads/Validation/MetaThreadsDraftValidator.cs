using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.MetaThreads.Validation;

public sealed class MetaThreadsDraftValidator : IDraftValidator
{
    public const int MaxCharacters = 500;
    public const int MaxMediaPerPost = 10;

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

        var doc = ThreadMarkdownDocument.Parse(content);
        var posts = new List<ValidateDraftPostResult>();
        bool hasErrors = false;
        bool hasWarnings = false;

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            var segDoc = doc.Segments[i];
            var segment = segDoc.RawMarkdown;
            var postNumber = i + 1;
            var issues = new List<ValidateDraftIssue>();

            var cleanedText = segDoc.PlainText;
            var mediaRefs = segDoc.MediaEmbeds;
            var imageCount = mediaRefs.Count;
            var hasYouTube = segDoc.YouTubeEmbed is not null;

            if (cleanedText.Length > MaxCharacters)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Post exceeds the {MaxCharacters}-character Threads limit by {cleanedText.Length - MaxCharacters} characters."));
            }

            if (imageCount > MaxMediaPerPost)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Threads posts support at most {MaxMediaPerPost} media attachments (carousel limit). Found {imageCount}."));
            }

            if (hasYouTube)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Warning,
                    "Threads does not support inline video players for YouTube embeds; URL will be published as text."));
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
                    "Markdown styling (bold, italic, headings) detected. Threads displays plain text; formatting symbols will be visible to readers."));
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
