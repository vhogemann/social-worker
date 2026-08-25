using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Validation;

public sealed class LinkedInDraftValidatorTests
{
    private readonly LinkedInDraftValidator _validator = new();

    [Fact]
    public void Validate_AllowsLongFormPost_UpTo3000Characters()
    {
        var text = new string('A', 2500);
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Single(result.Posts);
    }

    [Fact]
    public void Validate_Exceeding3000Characters_ReturnsError()
    {
        var text = new string('A', 3100);
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("3000-character"));
    }

    [Fact]
    public void Validate_MultiSegment_ReturnsErrorForLinkedIn()
    {
        var text = "Post 1 for LinkedIn\n\n---\n\nPost 2 for LinkedIn";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("multi-post threads"));
    }

    [Fact]
    public void Validate_MixedImageAndUrl_ReturnsError()
    {
        var mediaId = Guid.NewGuid();
        var text = $"Post with image ![Alt](media://{mediaId}) and link https://example.com";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("Cannot mix image attachments and URL link shares"));
    }

    [Fact]
    public void Validate_MarkdownStyling_ReturnsWarning()
    {
        var text = "This post has **bold text** for emphasis.";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("Markdown styling"));
    }
}
