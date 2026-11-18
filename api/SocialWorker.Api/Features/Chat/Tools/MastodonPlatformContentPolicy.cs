using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class MastodonPlatformContentPolicy : PlatformContentPolicyBase
{
    public const int MaxCharacters = 500;
    public const int MaxImages = 4;
    public const int UrlCharacterWeight = 23;

    private static readonly Regex UrlRegex = new(@"https?://[^\s<>()]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override SocialPlatform Platform => SocialPlatform.Mastodon;

    public static int CalculateCharacterCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return UrlRegex.Replace(text, _ => new string('x', UrlCharacterWeight)).Length;
    }

    public override string GetAdaptationRules()
    {
        return """
Mastodon rules:
- 500 characters per post maximum (every HTTP/HTTPS URL counts as 23 characters)
- Thoughtful, community-oriented, conversational tone
- Thread-first format: start with a clear hook, follow up with chained reply posts
- Clean text: no markdown formatting markers like bold or headings
- Media: up to 4 images OR 1 video/GIF per post (cannot mix video/GIF with images); always include descriptive alt text
- Accessibility: use CamelCase for multi-word hashtags (e.g. #OpenSource)
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
        var doc = ThreadMarkdownDocument.Parse(normalizedContent);
        foreach (var segment in doc.Segments)
        {
            var length = CalculateCharacterCount(segment.PlainText);
            if (length > MaxCharacters)
            {
                errors.Add($"Post exceeds {MaxCharacters} characters for {Platform} ({length}).");
            }

            if (segment.MediaEmbeds.Count > MaxImages)
            {
                errors.Add($"Post exceeds maximum of {MaxImages} media attachments for {Platform} ({segment.MediaEmbeds.Count}).");
            }
        }
    }
}
