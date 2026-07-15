using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record PlatformContentPolicyResult(
    bool IsValid,
    string NormalizedContent,
    List<string> Errors,
    List<string> Warnings);

public sealed class PlatformContentPolicy
{
    private static readonly Regex BoldAsteriskRegex = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
    private static readonly Regex BoldUnderscoreRegex = new(@"__([^_]+)__", RegexOptions.Compiled);
    private static readonly Regex ItalicAsteriskRegex = new(@"(?<!\*)\*([^*\n]+)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex HeadingRegex = new(@"(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);
    private static readonly Regex UnsupportedMarkdownRegex = new(@"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);
    private readonly BlueskyContentValidator _blueskyContentValidator;

    public PlatformContentPolicy(BlueskyContentValidator? blueskyContentValidator = null)
    {
        _blueskyContentValidator = blueskyContentValidator ?? new BlueskyContentValidator();
    }

    public PlatformContentPolicyResult Evaluate(SocialPlatform platform, string content, bool normalizeFormatting)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var normalized = normalizeFormatting ? Normalize(content) : content;

        if (UnsupportedMarkdownRegex.IsMatch(content))
        {
            warnings.Add("Markdown styling detected. Most platforms will show these markers as plain text.");
        }

        if (normalizeFormatting && !string.Equals(content, normalized, StringComparison.Ordinal))
        {
            warnings.Add("Unsupported markdown styling was removed during normalization.");
        }

        var maxChars = GetMaxChars(platform);
        if (maxChars.HasValue)
        {
            if (platform == SocialPlatform.Bluesky)
            {
                var segments = _blueskyContentValidator.Analyze(normalized);
                for (var i = 0; i < segments.Count; i++)
                {
                    if (segments[i].CharacterCount > maxChars.Value)
                    {
                        errors.Add($"Post {i + 1} exceeds {maxChars.Value} characters for {platform} ({segments[i].CharacterCount}).");
                    }
                }
            }
            else
            {
                var segments = DraftSegmentService.SplitMarkdownIntoSegments(normalized);
                for (var i = 0; i < segments.Count; i++)
                {
                    var cleaned = SharedPatterns.StripMediaMarkdown(segments[i]).Trim();
                    if (cleaned.Length > maxChars.Value)
                    {
                        errors.Add($"Post {i + 1} exceeds {maxChars.Value} characters for {platform} ({cleaned.Length}).");
                    }
                }
            }
        }

        return new PlatformContentPolicyResult(errors.Count == 0, normalized, errors, warnings);
    }

    public string GetAdaptationRules(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Twitter => """
Twitter rules:
- 280 characters per post maximum, 2-3 posts typical
- Punchy, conversational tone
- Break into short posts, each standalone
- Use hashtags sparingly (max 2)
- Reply threads: connect posts logically
""",
            SocialPlatform.LinkedIn => """
LinkedIn rules:
- ~3000 characters per post, 1-2 posts
- Professional tone
- Single long-form post or 2-part series
- Emojis used strategically
- Call-to-action at end
""",
            SocialPlatform.Instagram => """
Instagram rules:
- 2200 character caption limit, visual-first
- Lifestyle/visual tone, relatable
- Shorter sentences, more emojis
- Hashtags at end (5-10)
- Focus on visual story
""",
            SocialPlatform.Facebook => """
Facebook rules:
- No hard character limit, conversational
- Friendly, engaging tone
- Slightly longer form than Twitter
- Multi-generational audience (simpler language)
- Emojis welcome, moderate use
""",
            SocialPlatform.Bluesky => """
Bluesky rules:
- 300 characters per post maximum
- Keep copy concise and direct
- Prefer thread segmentation with --- on separate lines
""",
            _ => string.Empty
        };
    }

    private static int? GetMaxChars(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Bluesky => 300,
            SocialPlatform.Twitter => 280,
            SocialPlatform.LinkedIn => 3000,
            SocialPlatform.Instagram => 2200,
            SocialPlatform.Facebook => null,
            _ => null
        };
    }

    private static string Normalize(string content)
    {
        var normalized = content;
        normalized = BoldAsteriskRegex.Replace(normalized, "$1");
        normalized = BoldUnderscoreRegex.Replace(normalized, "$1");
        normalized = ItalicAsteriskRegex.Replace(normalized, "$1");
        normalized = HeadingRegex.Replace(normalized, "");
        return normalized;
    }
}