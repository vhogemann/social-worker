using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class PlatformContentPolicyTests
{
    [Fact]
    public void GetAdaptationRules_ReturnsTwitterLimits()
    {
        var policy = new PlatformContentPolicy();

        var rules = policy.GetAdaptationRules(SocialPlatform.Twitter);

        Assert.Contains("280 characters", rules);
        Assert.Contains("hashtags", rules);
    }

    [Fact]
    public void GetAdaptationRules_ReturnsBlueskyLimits()
    {
        var policy = new PlatformContentPolicy();

        var rules = policy.GetAdaptationRules(SocialPlatform.Bluesky);

        Assert.Contains("300 characters", rules);
        Assert.Contains("---", rules);
    }
}