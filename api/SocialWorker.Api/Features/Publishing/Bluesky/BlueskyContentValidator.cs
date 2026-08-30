using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Infrastructure.Markdown;

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

    public List<BlueskySegmentValidation> Analyze(string content)
    {
        return Analyze(ThreadMarkdownDocument.Parse(content));
    }

    public List<BlueskySegmentValidation> Analyze(ThreadMarkdownDocument doc)
    {
        var results = new List<BlueskySegmentValidation>(doc.Segments.Count);

        foreach (var segment in doc.Segments)
        {
            results.Add(new BlueskySegmentValidation(
                Segment: segment.RawMarkdown,
                CleanedText: segment.PlainText,
                CharacterCount: segment.CharacterCount,
                ImageCount: segment.MediaEmbeds.Count,
                HasYouTube: segment.HasYouTube,
                HasUnsupportedMarkdown: segment.HasUnsupportedFormatting));

        }

        return results;
    }

    public string? GetFirstPublishValidationError(string content)
    {
        return GetFirstPublishValidationError(ThreadMarkdownDocument.Parse(content));
    }

    public string? GetFirstPublishValidationError(ThreadMarkdownDocument doc)
    {
        var segments = Analyze(doc);

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