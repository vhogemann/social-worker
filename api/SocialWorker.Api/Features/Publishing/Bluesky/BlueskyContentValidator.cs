using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed record BlueskySegmentValidation(
    string Segment,
    string CleanedText,
    int CharacterCount,
    int ImageCount,
    bool HasYouTube,
    bool HasUnsupportedMarkdown);

public sealed class BlueskyContentValidator
{
    private const int MaxCharactersPerPost = 300;
    private const int MaxImagesPerPost = 4;

    private static readonly Regex UnsupportedBlueskyMarkdownRegex = new(@"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);

    public List<BlueskySegmentValidation> Analyze(string content)
    {
        var segments = DraftSegmentService.SplitMarkdownIntoSegments(content);
        var results = new List<BlueskySegmentValidation>(segments.Count);

        foreach (var segment in segments)
        {
            var cleanedText = SharedPatterns.StripMediaMarkdown(segment).Trim();
            var imageCount = SharedPatterns.CountMediaReferences(segment);
            var hasYouTube = segment.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                             segment.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
            var hasUnsupportedMarkdown = UnsupportedBlueskyMarkdownRegex.IsMatch(segment);

            results.Add(new BlueskySegmentValidation(
                Segment: segment,
                CleanedText: cleanedText,
                CharacterCount: cleanedText.Length,
                ImageCount: imageCount,
                HasYouTube: hasYouTube,
                HasUnsupportedMarkdown: hasUnsupportedMarkdown));
        }

        return results;
    }

    public string? GetFirstPublishValidationError(string content)
    {
        var segments = Analyze(content);

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment.CleanedText))
            {
                continue;
            }

            if (segment.CharacterCount > MaxCharactersPerPost)
            {
                return $"Post exceeds {MaxCharactersPerPost} character limit ({segment.CharacterCount} characters). Please shorten the content.";
            }

            if (segment.ImageCount > MaxImagesPerPost)
            {
                return $"Post contains {segment.ImageCount} images. Bluesky allows maximum {MaxImagesPerPost} images per post.";
            }

            if (segment.ImageCount > 0 && segment.HasYouTube)
            {
                return "Cannot mix images and YouTube embeds in a single post on Bluesky.";
            }

            if (segment.HasUnsupportedMarkdown)
            {
                return "Post contains unsupported markdown (bold/italic/heading markers). Use plain text only.";
            }
        }

        return null;
    }
}