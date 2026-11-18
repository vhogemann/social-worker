using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.Mastodon.Validation;

public sealed class MastodonDraftValidator : IDraftValidator
{
    public const int MaxCharacters = MastodonPlatformContentPolicy.MaxCharacters;
    public const int MaxImagesPerPost = MastodonPlatformContentPolicy.MaxImages;

    private static readonly Regex MarkdownStylingRegex = new(
        @"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+",
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
        var assetById = (mediaAssets ?? Array.Empty<MediaAsset>()).ToDictionary(a => a.Id);
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
            var textToMeasure = cleanedText;
            if (segDoc.YouTubeEmbed != null)
            {
                textToMeasure = string.IsNullOrWhiteSpace(textToMeasure)
                    ? segDoc.YouTubeEmbed.Url
                    : $"{textToMeasure}\n\n{segDoc.YouTubeEmbed.Url}";
            }

            var effectiveLength = MastodonPlatformContentPolicy.CalculateCharacterCount(textToMeasure);
            var mediaRefs = segDoc.MediaEmbeds;
            var mediaCount = mediaRefs.Count;
            var hasYouTube = segDoc.YouTubeEmbed is not null || segDoc.HasYouTube;

            if (effectiveLength > MaxCharacters)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Post exceeds the {MaxCharacters}-character Mastodon limit by {effectiveLength - MaxCharacters} characters."));
            }

            var hasVideoOrGif = mediaRefs.Any(r => IsVideoOrGif(r, assetById));
            if (hasVideoOrGif && mediaCount > 1)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    "Mastodon allows at most 1 video or GIF per post and cannot mix video/GIF with images."));
            }
            else if (mediaCount > MaxImagesPerPost)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Error,
                    $"Mastodon allows at most {MaxImagesPerPost} images per post (found {mediaCount})."));
            }

            if (hasYouTube)
            {
                issues.Add(new ValidateDraftIssue(
                    ValidateDraftSeverity.Warning,
                    "Mastodon does not support inline video players for YouTube embeds; URL will be published as text."));
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
                    "Markdown styling (bold, italic, headings) detected. Mastodon displays plain text; formatting symbols will be visible to readers."));
            }

            foreach (var mediaRef in mediaRefs)
            {
                if (string.IsNullOrWhiteSpace(mediaRef.AltText))
                {
                    assetById.TryGetValue(mediaRef.MediaId, out var asset);
                    if (asset == null || string.IsNullOrWhiteSpace(asset.AltText))
                    {
                        var name = asset?.FileName ?? mediaRef.MediaId.ToString();
                        issues.Add(new ValidateDraftIssue(
                            ValidateDraftSeverity.Warning,
                            $"Missing ALT text on media: {name}."));
                    }
                }
            }

            if (issues.Any(x => x.Severity == ValidateDraftSeverity.Error)) hasErrors = true;
            if (issues.Any(x => x.Severity == ValidateDraftSeverity.Warning)) hasWarnings = true;

            posts.Add(new ValidateDraftPostResult(
                postNumber,
                effectiveLength,
                mediaCount,
                hasYouTube,
                issues));
        }

        var status = hasErrors
            ? ValidateDraftOverallStatus.Failed
            : hasWarnings
                ? ValidateDraftOverallStatus.Warnings
                : ValidateDraftOverallStatus.Valid;

        return new ValidateDraftResult(posts, status);
    }

    private static bool IsVideoOrGif(MediaEmbedReference mediaRef, IReadOnlyDictionary<Guid, MediaAsset> assetById)
    {
        if (assetById.TryGetValue(mediaRef.MediaId, out var asset))
        {
            if (asset.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                asset.MimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var target = asset.FileName;
            return target.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   target.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
                   target.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ||
                   target.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
