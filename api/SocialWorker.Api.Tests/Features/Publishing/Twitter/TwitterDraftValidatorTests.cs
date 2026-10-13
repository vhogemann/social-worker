using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Twitter.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Twitter;

public class TwitterDraftValidatorTests
{
    private readonly TwitterDraftValidator _validator = new();

    [Fact]
    public void Validate_EmptyContent_ReturnsFailed()
    {
        var result = _validator.Validate("", Array.Empty<MediaAsset>());
        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Single(result.Posts);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("cannot be empty"));
    }

    [Fact]
    public void Validate_StandardTweet_ReturnsValid()
    {
        var content = "This is a great update for everyone following our release!";
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.Single(result.Posts);
        Assert.Empty(result.Posts[0].Issues);
        Assert.Equal(content.Length, result.Posts[0].CharacterCount);
    }

    [Fact]
    public void Validate_Exceeds280WeightedCharacters_ReturnsFailed()
    {
        var content = new string('x', 281);
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Single(result.Posts);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("280 weighted-character limit"));
    }

    [Fact]
    public void Validate_LongUrlWithin280Budget_ReturnsValid()
    {
        // 200 ascii chars + 150 char URL = 350 raw chars, but only 200 + 23 = 223 weighted units!
        var prefix = new string('a', 200);
        var longUrl = "https://example.com/a/very/long/path/with/lots/of/parameters/that/exceeds/the/old/raw/limit?id=123456789&key=abcdefghijk";
        var content = $"{prefix} {longUrl}";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.Equal(200 + 1 + 23, result.Posts[0].CharacterCount);
    }

    [Fact]
    public void Validate_MediaAttachment_UpToFour_ReturnsValid()
    {
        var content = "Look at this chart:\n\n![Chart](media://61803512-ef24-4f81-a5d6-ec2d790757f5)";
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.Empty(result.Posts[0].Issues);
        Assert.Equal(1, result.Posts[0].ImageCount);
    }

    [Fact]
    public void Validate_MediaAttachment_ExceedsFour_ReturnsFailed()
    {
        var content = "Too many images:\n\n" +
                      "![Img1](media://61803512-ef24-4f81-a5d6-ec2d790757f1)\n" +
                      "![Img2](media://61803512-ef24-4f81-a5d6-ec2d790757f2)\n" +
                      "![Img3](media://61803512-ef24-4f81-a5d6-ec2d790757f3)\n" +
                      "![Img4](media://61803512-ef24-4f81-a5d6-ec2d790757f4)\n" +
                      "![Img5](media://61803512-ef24-4f81-a5d6-ec2d790757f5)";
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("maximum of 4 images per tweet"));
    }

    [Fact]
    public void Validate_MarkdownLinks_ReturnsWarning()
    {
        var content = "Check out [our website](https://example.com) for more!";
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("Markdown link '[our website](https://example.com)' detected"));
    }

    [Fact]
    public void Validate_MarkdownStyling_ReturnsWarning()
    {
        var content = "This is **very important** information.";
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("Markdown styling"));
    }

    [Fact]
    public void Validate_PlaceholderLink_ReturnsFailed()
    {
        var content = "Read more here: [insert link](https://example.com)";
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("unreplaced placeholder"));
    }

    [Fact]
    public void Validate_MultiPostThread_ValidatesEachPost()
    {
        var content = "Post one\n\n---\n\nPost two with **styling**\n\n---\n\nPost three " + new string('z', 280);
        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Equal(3, result.Posts.Count);
        Assert.Empty(result.Posts[0].Issues);
        Assert.Contains(result.Posts[1].Issues, i => i.Severity == ValidateDraftSeverity.Warning);
        Assert.Contains(result.Posts[2].Issues, i => i.Severity == ValidateDraftSeverity.Error);
    }
}
