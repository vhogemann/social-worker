using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Chat.Tools;

public interface IPlatformContentPolicy
{
    SocialPlatform Platform { get; }

    PlatformContentPolicyResult Evaluate(string content, bool normalizeFormatting);

    string GetAdaptationRules();
}