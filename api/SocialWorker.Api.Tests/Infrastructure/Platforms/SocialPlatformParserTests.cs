using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Platforms;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure.Platforms;

public sealed class SocialPlatformParserTests
{
    [Theory]
    [InlineData("threads", SocialPlatform.MetaThreads)]
    [InlineData("Threads", SocialPlatform.MetaThreads)]
    [InlineData("THREADS", SocialPlatform.MetaThreads)]
    [InlineData("meta threads", SocialPlatform.MetaThreads)]
    [InlineData("Meta Threads", SocialPlatform.MetaThreads)]
    [InlineData("metathreads", SocialPlatform.MetaThreads)]
    [InlineData("MetaThreads", SocialPlatform.MetaThreads)]
    [InlineData("bluesky", SocialPlatform.Bluesky)]
    [InlineData("Bluesky", SocialPlatform.Bluesky)]
    [InlineData("twitter", SocialPlatform.Twitter)]
    [InlineData("x", SocialPlatform.Twitter)]
    [InlineData("linkedin", SocialPlatform.LinkedIn)]
    [InlineData("facebook", SocialPlatform.Facebook)]
    [InlineData("instagram", SocialPlatform.Instagram)]
    public void TryParse_ValidInput_ReturnsExpectedPlatform(string input, SocialPlatform expected)
    {
        var result = SocialPlatformParser.TryParse(input, out var platform);

        Assert.True(result);
        Assert.Equal(expected, platform);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("mastodon")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        var result = SocialPlatformParser.TryParse(input, out var platform);

        Assert.False(result);
        Assert.Equal(default, platform);
    }
}
