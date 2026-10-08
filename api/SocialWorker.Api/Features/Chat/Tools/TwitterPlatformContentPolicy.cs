using System.Collections.Generic;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class TwitterPlatformContentPolicy : PlatformContentPolicyBase
{
    public override SocialPlatform Platform => SocialPlatform.Twitter;

    public override string GetAdaptationRules()
    {
        return """
Twitter rules:
- 280 characters per post maximum (weighted), 2-3 posts typical
- URLs consume 23 characters each (budgeted via t.co)
- Plain text only: markdown formatting (bold, italic, headers, anchor links) is not rendered
- Punchy, conversational tone
- Break into short posts, each standalone
- Use hashtags sparingly (max 2)
- Reply threads: connect posts logically
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
        var doc = SocialWorker.Api.Infrastructure.Markdown.ThreadMarkdownDocument.Parse(normalizedContent);
        foreach (var segment in doc.Segments)
        {
            var cleaned = segment.PlainText;
            var weight = SocialWorker.Api.Features.Publishing.Twitter.TwitterTextWeightCalculator.CalculateWeight(cleaned);
            if (weight > 280)
            {
                errors.Add($"Post exceeds 280 characters for {Platform} ({weight}).");
            }
        }
    }
}