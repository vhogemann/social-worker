using SocialWorker.Api.Features.Publishing.Bluesky;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing;

public sealed class BlueskyContentValidatorTests
{
    [Fact]
    public void GetFirstPublishValidationError_ReturnsNull_ForValidContent()
    {
        var validator = new BlueskyContentValidator();

        var error = validator.GetFirstPublishValidationError("Hello world\n---\nAnother post with #hashtag");

        Assert.Null(error);
    }

    [Fact]
    public void GetFirstPublishValidationError_ReturnsCharacterLimitError_WhenSegmentTooLong()
    {
        var validator = new BlueskyContentValidator();
        var longText = new string('x', 301);

        var error = validator.GetFirstPublishValidationError(longText);

        Assert.Equal("Post exceeds 300 character limit (301 characters). Please shorten the content.", error);
    }

    [Fact]
    public void GetFirstPublishValidationError_ReturnsMixedMediaError_WhenImageAndYoutubePresent()
    {
        var validator = new BlueskyContentValidator();
        var content = $"Post with image ![img](media://11111111-1111-1111-1111-111111111111) and youtube https://youtube.com/watch?v=abc";

        var error = validator.GetFirstPublishValidationError(content);

        Assert.Equal("Cannot mix images and YouTube embeds in a single post on Bluesky.", error);
    }

    [Fact]
    public void Analyze_ReturnsSegmentMetrics_ForAllSegments()
    {
        var validator = new BlueskyContentValidator();
        var content = "First\n---\nSecond with **bold**";

        var result = validator.Analyze(content);

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result[0].CharacterCount);
        Assert.True(result[1].HasUnsupportedMarkdown);
    }
}
