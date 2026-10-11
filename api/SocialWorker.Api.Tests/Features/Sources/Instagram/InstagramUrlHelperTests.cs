using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources.Instagram;

public sealed class InstagramUrlHelperTests
{
    [Theory]
    [InlineData("https://www.instagram.com/flaviobolsonaro", true)]
    [InlineData("https://instagram.com/jairmessiasbolsonaro/", true)]
    [InlineData("http://instagram.com/lulaoficial", true)]
    [InlineData("instagram://cartacapital", true)]
    [InlineData("https://youtube.com/channel/123", false)]
    [InlineData("https://threads.net/@vhogemann", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsInstagramUrl_IdentifiesInstagramSources(string? url, bool expected)
    {
        var result = InstagramUrlHelper.IsInstagramUrl(url);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://www.instagram.com/flaviobolsonaro", "flaviobolsonaro")]
    [InlineData("https://www.instagram.com/flaviobolsonaro/", "flaviobolsonaro")]
    [InlineData("https://instagram.com/jairmessiasbolsonaro", "jairmessiasbolsonaro")]
    [InlineData("instagram://lulaoficial", "lulaoficial")]
    [InlineData("@cartacapital", "cartacapital")]
    [InlineData("https://www.instagram.com/p/DB123456", null)]
    [InlineData("https://www.instagram.com/reel/DB123456", null)]
    [InlineData("https://www.instagram.com/stories/user", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractUsername_ExtractsCorrectUsername(string? url, string? expected)
    {
        var result = InstagramUrlHelper.ExtractUsername(url);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFeedUrl_FormatsAsInstagramScheme()
    {
        var result = InstagramUrlHelper.NormalizeFeedUrl("@flaviobolsonaro");
        Assert.Equal("instagram://flaviobolsonaro", result);
    }

    [Fact]
    public void GetWebUrl_FormatsAsHttpsInstagramUrl()
    {
        var result = InstagramUrlHelper.GetWebUrl("@flaviobolsonaro");
        Assert.Equal("https://www.instagram.com/flaviobolsonaro", result);
    }
}
