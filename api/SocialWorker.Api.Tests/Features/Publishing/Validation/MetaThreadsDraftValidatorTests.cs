using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.MetaThreads.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Validation;

public sealed class MetaThreadsDraftValidatorTests
{
    private readonly MetaThreadsDraftValidator _validator = new();

    [Fact]
    public void Validate_AllowsPost_UpTo500Characters()
    {
        var text = new string('A', 480);
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Single(result.Posts);
    }

    [Fact]
    public void Validate_Exceeding500Characters_ReturnsError()
    {
        var text = new string('A', 505);
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("500-character"));
    }

    [Fact]
    public void Validate_MultiSegment_AllowedForMetaThreads()
    {
        var text = "First segment of a Threads thread.\n\n---\n\nSecond segment of the thread.";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Equal(2, result.Posts.Count);
    }

    [Fact]
    public void Validate_CarouselWithUpTo10Images_Allowed()
    {
        var embeds = new List<string>();
        var mediaAssets = new List<MediaAsset>();
        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            embeds.Add($"![Image {i}](media://{id})");
            mediaAssets.Add(new MediaAsset
            {
                Id = id,
                AltText = $"Description {i}",
                FileName = $"img{i}.jpg",
                FilePath = $"img{i}.jpg",
                MimeType = "image/jpeg",
                SizeBytes = 1000
            });
        }

        var text = "Thread post with carousel:\n" + string.Join("\n", embeds);
        var result = _validator.Validate(text, mediaAssets);

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Equal(5, result.Posts[0].ImageCount);
    }

    [Fact]
    public void Validate_Exceeding10Images_ReturnsError()
    {
        var embeds = new List<string>();
        for (int i = 0; i < 11; i++)
        {
            embeds.Add($"![Image {i}](media://{Guid.NewGuid()})");
        }

        var text = "Post with 11 images:\n" + string.Join("\n", embeds);
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("10 media attachments"));
    }

    [Fact]
    public void Validate_MissingAltText_ReturnsWarning()
    {
        var mediaId = Guid.NewGuid();
        var text = $"Post with image ![ ](media://{mediaId})";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("Missing ALT text"));
    }

    [Fact]
    public void Validate_PlaceholderLink_ReturnsError()
    {
        var text = "Check out this link: [insert link](placeholder)";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("placeholder links"));
    }

    [Fact]
    public void Validate_MarkdownStyling_ReturnsWarning()
    {
        var text = "Post with **bold emphasis** and *italics*.";
        var result = _validator.Validate(text, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("Markdown styling"));
    }

    [Fact]
    public void Validate_EmptyContent_ReturnsError()
    {
        var result = _validator.Validate("", Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("Draft content cannot be empty"));
    }
}
