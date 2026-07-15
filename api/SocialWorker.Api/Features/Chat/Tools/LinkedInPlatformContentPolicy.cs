using System.Collections.Generic;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class LinkedInPlatformContentPolicy : PlatformContentPolicyBase
{
    public override SocialPlatform Platform => SocialPlatform.LinkedIn;

    public override string GetAdaptationRules()
    {
        return """
LinkedIn rules:
- ~3000 characters per post, 1-2 posts
- Professional tone
- Single long-form post or 2-part series
- Emojis used strategically
- Call-to-action at end
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
        foreach (var segment in SplitSegments(normalizedContent))
        {
            var cleaned = SharedPatterns.StripMediaMarkdown(segment).Trim();
            if (cleaned.Length > 3000)
            {
                errors.Add($"Post exceeds 3000 characters for {Platform} ({cleaned.Length}).");
            }
        }
    }
}