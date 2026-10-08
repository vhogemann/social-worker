using SocialWorker.Api.Features.Publishing.Twitter;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Twitter;

public class TwitterTextWeightCalculatorTests
{
    [Fact]
    public void CalculateWeight_NullOrEmpty_ReturnsZero()
    {
        Assert.Equal(0, TwitterTextWeightCalculator.CalculateWeight(null));
        Assert.Equal(0, TwitterTextWeightCalculator.CalculateWeight(""));
    }

    [Fact]
    public void CalculateWeight_StandardAscii_CountsOnePerChar()
    {
        var text = "Hello world!";
        Assert.Equal(12, TwitterTextWeightCalculator.CalculateWeight(text));
    }

    [Fact]
    public void CalculateWeight_Urls_AlwaysCountAs23Units()
    {
        var shortUrl = "https://a.co";
        var longUrl = "https://example.com/very/long/path/with/lots/of/query/parameters?param1=value1&param2=value2";

        Assert.Equal(23, TwitterTextWeightCalculator.CalculateWeight(shortUrl));
        Assert.Equal(23, TwitterTextWeightCalculator.CalculateWeight(longUrl));
    }

    [Fact]
    public void CalculateWeight_TextWithMultipleUrls_CountsCorrectly()
    {
        var text = "Read this: https://example.com and then check https://x.com/news for updates!";
        // "Read this: " -> 11 chars
        // "https://example.com" -> 23 units
        // " and then check " -> 16 chars
        // "https://x.com/news" -> 23 units
        // " for updates!" -> 13 chars
        // Total = 11 + 23 + 16 + 23 + 13 = 86
        Assert.Equal(86, TwitterTextWeightCalculator.CalculateWeight(text));
    }

    [Fact]
    public void CalculateWeight_CjkCharacters_CountAsTwo()
    {
        // 5 Chinese characters
        var text = "你好世界啊";
        Assert.Equal(10, TwitterTextWeightCalculator.CalculateWeight(text));
    }

    [Fact]
    public void CalculateWeight_Emojis_CountAsTwo()
    {
        // Rocket emoji (surrogate pair)
        var text = "🚀";
        Assert.Equal(2, TwitterTextWeightCalculator.CalculateWeight(text));

        // Mixed text + emoji
        var mixed = "Launch 🚀 now!";
        // "Launch " (7) + emoji (2) + " now!" (5) = 14
        Assert.Equal(14, TwitterTextWeightCalculator.CalculateWeight(mixed));
    }

    [Fact]
    public void IsValid_Within280_ReturnsTrue()
    {
        var text = new string('a', 280);
        Assert.True(TwitterTextWeightCalculator.IsValid(text));
        Assert.Equal(0, TwitterTextWeightCalculator.GetRemainingUnits(text));
    }

    [Fact]
    public void IsValid_Exceeds280_ReturnsFalse()
    {
        var text = new string('a', 281);
        Assert.False(TwitterTextWeightCalculator.IsValid(text));
        Assert.Equal(-1, TwitterTextWeightCalculator.GetRemainingUnits(text));
    }
}
