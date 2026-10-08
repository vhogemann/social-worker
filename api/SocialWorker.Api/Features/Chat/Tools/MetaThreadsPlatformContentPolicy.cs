using System.Collections.Generic;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class MetaThreadsPlatformContentPolicy : PlatformContentPolicyBase
{
    public const int MaxCharacters = 500;
    public const int MaxMedia = 10;

    public override SocialPlatform Platform => SocialPlatform.MetaThreads;

    public override string GetAdaptationRules()
    {
        return """
Threads rules:
- 500 characters per post maximum
- Conversational, approachable, thought-provoking tone
- Thread-first format: start with a hook, follow up with connected reply posts
- Clean text: no markdown formatting markers like bold/headings
- Media: supports up to 10 images/videos in a carousel
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
        var doc = ThreadMarkdownDocument.Parse(normalizedContent);
        foreach (var segment in doc.Segments)
        {
            var cleaned = segment.PlainText;
            if (cleaned.Length > MaxCharacters)
            {
                errors.Add($"Post exceeds {MaxCharacters} characters for {Platform} ({cleaned.Length}).");
            }

            if (segment.MediaEmbeds.Count > MaxMedia)
            {
                errors.Add($"Post exceeds maximum of {MaxMedia} media attachments for {Platform} ({segment.MediaEmbeds.Count}).");
            }
        }
    }
}
