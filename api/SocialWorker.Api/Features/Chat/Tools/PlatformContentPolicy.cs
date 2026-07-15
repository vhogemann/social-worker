using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record PlatformContentPolicyResult(
    bool IsValid,
    string NormalizedContent,
    List<string> Errors,
    List<string> Warnings);

public sealed class PlatformContentPolicy
{
    private readonly IReadOnlyDictionary<SocialPlatform, IPlatformContentPolicy> _policies;

    public PlatformContentPolicy()
        : this(new IPlatformContentPolicy[]
        {
            new SocialWorker.Api.Features.Publishing.Bluesky.Validation.BlueskyPlatformContentPolicy(),
            new TwitterPlatformContentPolicy(),
            new LinkedInPlatformContentPolicy(),
            new InstagramPlatformContentPolicy(),
            new FacebookPlatformContentPolicy()
        })
    {
    }

    public PlatformContentPolicy(IEnumerable<IPlatformContentPolicy> policies)
    {
        _policies = policies.ToDictionary(policy => policy.Platform);
    }

    public PlatformContentPolicyResult Evaluate(SocialPlatform platform, string content, bool normalizeFormatting)
    {
        return GetPolicy(platform).Evaluate(content, normalizeFormatting);
    }

    public string GetAdaptationRules(SocialPlatform platform)
    {
        return GetPolicy(platform).GetAdaptationRules();
    }

    private IPlatformContentPolicy GetPolicy(SocialPlatform platform)
    {
        return _policies.TryGetValue(platform, out var policy)
            ? policy
            : new EmptyPlatformContentPolicy(platform);
    }

    private sealed class EmptyPlatformContentPolicy : IPlatformContentPolicy
    {
        public EmptyPlatformContentPolicy(SocialPlatform platform)
        {
            Platform = platform;
        }

        public SocialPlatform Platform { get; }

        public PlatformContentPolicyResult Evaluate(string content, bool normalizeFormatting)
        {
            return new PlatformContentPolicyResult(true, content, new List<string>(), new List<string>());
        }

        public string GetAdaptationRules()
        {
            return string.Empty;
        }
    }
}