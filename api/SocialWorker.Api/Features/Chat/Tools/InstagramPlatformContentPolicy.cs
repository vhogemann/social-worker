using System.Collections.Generic;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class InstagramPlatformContentPolicy : PlatformContentPolicyBase
{
    public override SocialPlatform Platform => SocialPlatform.Instagram;

    public override string GetAdaptationRules()
    {
        return """
Instagram rules:
- 2200 character caption limit, visual-first
- Lifestyle/visual tone, relatable
- Shorter sentences, more emojis
- Hashtags at end (5-10)
- Focus on visual story
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
        foreach (var segment in SplitSegments(normalizedContent))
        {
            var cleaned = SharedPatterns.StripMediaMarkdown(segment).Trim();
            if (cleaned.Length > 2200)
            {
                errors.Add($"Post exceeds 2200 characters for {Platform} ({cleaned.Length}).");
            }
        }
    }
}