using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.Instagram.Validation;

public sealed class InstagramDraftValidator : IDraftValidator
{
    public const int MaxCaptionCharacters = 2200;
    public const int MaxHashtags = 30;
    public const int MaxCarouselItems = 10;
    public const double MinAspectRatio = 0.8;
    public const double MaxAspectRatio = 1.91;

    private static readonly Regex HashtagRegex = new(@"(?<!\w)#[\p{L}\p{N}_]+", RegexOptions.Compiled);
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
        var assetById = mediaAssets.ToDictionary(a => a.Id);
        var posts = new List<ValidateDraftPostResult>();
        bool hasErrors = false;
        bool hasWarnings = false;

        int totalImages = doc.Segments.Sum(s => s.MediaEmbeds.Count);
        bool isMultiSegment = doc.Segments.Count > 1;

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            var segDoc = doc.Segments[i];
            var segment = segDoc.RawMarkdown;
            var postNumber = i + 1;
            var issues = new List<ValidateDraftIssue>();

            var cleanedText = segDoc.PlainText;
            var textToMeasure = cleanedText;
            if (segDoc.YouTubeEmbed != null)
            {
                textToMeasure = string.IsNullOrWhiteSpace(textToMeasure)
                    ? segDoc.YouTubeEmbed.Url
                    : $"{textToMeasure}\n\n{segDoc.YouTubeEmbed.Url}";
            }

            var mediaRefs = segDoc.MediaEmbeds;
            var imageCount = mediaRefs.Count;
            var hasYouTube = segDoc.YouTubeEmbed is not null;

            if (i == 0)
            {
                if (totalImages == 0)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Error,
                        "Instagram requires at least 1 image attachment."));
                }

                if (totalImages > MaxCarouselItems)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Error,
                        $"Instagram carousels support at most {MaxCarouselItems} images. Found {totalImages}."));
                }

                if (textToMeasure.Length > MaxCaptionCharacters)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Error,
                        $"Caption exceeds the {MaxCaptionCharacters}-character Instagram limit by {textToMeasure.Length - MaxCaptionCharacters} characters."));
                }

                var hashtagCount = HashtagRegex.Matches(textToMeasure).Count;
                if (hashtagCount > MaxHashtags)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Warning,
                        $"Caption contains {hashtagCount} hashtags; Instagram allows at most {MaxHashtags} hashtags."));
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(cleanedText) || hasYouTube)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Error,
                        $"Instagram carousel captions come only from segment 1. Consolidate or remove text in segment {postNumber}."));
                }

                if (isMultiSegment && imageCount == 0)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Error,
                        $"Carousel segment {postNumber} must include at least 1 image."));
                }
            }

            if (isMultiSegment && i == 0 && imageCount == 0 && totalImages > 0)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "Carousel segment 1 must include at least 1 image."));
            }

            if (hasYouTube)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Warning,
                    "Instagram does not support inline YouTube video embeds; the URL will appear as plain text in the caption."));
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
                    "Markdown styling (bold, italic, headings) detected. Instagram displays plain text; formatting symbols will be visible to readers."));
            }

            foreach (var mediaRef in mediaRefs)
            {
                if (assetById.TryGetValue(mediaRef.MediaId, out var asset))
                {
                    if (!string.IsNullOrWhiteSpace(asset.MimeType) &&
                        !asset.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidateDraftIssue(
                            ValidateDraftSeverity.Error,
                            $"Instagram publishing currently supports images only; '{asset.FileName}' has MIME type '{asset.MimeType}'."));
                    }

                    if (asset.Width > 0 && asset.Height > 0)
                    {
                        var ratio = (double)asset.Width / asset.Height;
                        if (ratio < MinAspectRatio || ratio > MaxAspectRatio)
                        {
                            issues.Add(new ValidateDraftIssue(
                                ValidateDraftSeverity.Warning,
                                $"Image '{asset.FileName}' has aspect ratio {ratio:F2}:1 ({asset.Width}x{asset.Height}), outside Instagram's recommended 4:5 (0.80) to 1.91:1 range."));
                        }
                    }
                }

                var effectiveAltText = !string.IsNullOrWhiteSpace(mediaRef.AltText)
                    ? mediaRef.AltText
                    : asset?.AltText;
                if (string.IsNullOrWhiteSpace(effectiveAltText))
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Warning,
                        "Image has no ALT text. Consider adding descriptive alt text for accessibility."));
                }
            }

            if (issues.Any(x => x.Severity == ValidateDraftSeverity.Error)) hasErrors = true;
            if (issues.Any(x => x.Severity == ValidateDraftSeverity.Warning)) hasWarnings = true;

            posts.Add(new ValidateDraftPostResult(
                postNumber,
                textToMeasure.Length,
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
