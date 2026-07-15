using System.Collections.Generic;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class FacebookPlatformContentPolicy : PlatformContentPolicyBase
{
    public override SocialPlatform Platform => SocialPlatform.Facebook;

    public override string GetAdaptationRules()
    {
        return """
Facebook rules:
- No hard character limit, conversational
- Friendly, engaging tone
- Slightly longer form than Twitter
- Multi-generational audience (simpler language)
- Emojis welcome, moderate use
""";
    }

    protected override void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings)
    {
    }
}