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
- 280 characters per post maximum, 2-3 posts typical
- Punchy, conversational tone
- Break into short posts, each standalone
- Use hashtags sparingly (max 2)
- Reply threads: connect posts logically
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
        foreach (var segment in SplitSegments(normalizedContent))
        {
            var cleaned = SharedPatterns.StripMediaMarkdown(segment).Trim();
            if (cleaned.Length > 280)
            {
                errors.Add($"Post exceeds 280 characters for {Platform} ({cleaned.Length}).");
            }
        }
    }
}