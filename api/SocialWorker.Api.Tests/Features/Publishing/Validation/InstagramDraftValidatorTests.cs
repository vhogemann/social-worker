using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Instagram.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Validation;

public sealed class InstagramDraftValidatorTests
{
    private readonly InstagramDraftValidator _validator = new();

    private static MediaAsset CreateImage(Guid id, int width = 1080, int height = 1080, string? altText = "Alt", string mimeType = "image/jpeg")
    {
        return new MediaAsset
        {
            Id = id,
            FileName = "img.jpg",
            MimeType = mimeType,
            AltText = altText,
            Width = width,
            Height = height
        };
    }

    [Fact]
    public void Validate_ValidSingleImagePost_Passes()
    {
        var id = Guid.NewGuid();
        var content = $"A crisp square photo!\n![Square photo](media://{id})";
        var result = _validator.Validate(content, new[] { CreateImage(id) });

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.Empty(result.Posts[0].Issues);
    }

    [Fact]
    public void Validate_NoImages_FailsWithRequiredImageError()
    {
        var result = _validator.Validate("Just text, no image.", Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("at least 1 image"));
    }

    [Fact]
    public void Validate_CaptionExceeds2200Chars_Fails()
    {
        var id = Guid.NewGuid();
        var longCaption = new string('a', 2201);
        var result = _validator.Validate($"{longCaption}\n![Alt](media://{id})", new[] { CreateImage(id) });

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("2200-character"));
    }

    [Fact]
    public void Validate_MoreThan10Images_Fails()
    {
        var assets = new List<MediaAsset>();
        var embeds = new List<string>();
        for (int i = 0; i < 11; i++)
        {
            var id = Guid.NewGuid();
            assets.Add(CreateImage(id));
            embeds.Add($"![Slide {i + 1}](media://{id})");
        }

        var content = "Carousel\n" + string.Join("\n", embeds);
        var result = _validator.Validate(content, assets);

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("at most 10 images"));
    }

    [Fact]
    public void Validate_MultiSegmentWithTextInSecondSegment_Fails()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var content = $"Main caption\n![First](media://{id1})\n---\nSecond segment text\n![Second](media://{id2})";
        var result = _validator.Validate(content, new[] { CreateImage(id1), CreateImage(id2) });

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[1].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("segment 2"));
    }

    [Fact]
    public void Validate_MultiSegmentWithImageOnlyInSecondSegment_Passes()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var content = $"Main caption\n![First](media://{id1})\n---\n![Second](media://{id2})";
        var result = _validator.Validate(content, new[] { CreateImage(id1), CreateImage(id2) });

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
    }

    [Fact]
    public void Validate_AspectRatioOutsideRange_PassesWithWarning()
    {
        var id = Guid.NewGuid();
        var content = $"Wide panorama\n![Wide](media://{id})";
        var result = _validator.Validate(content, new[] { CreateImage(id, width: 2500, height: 1000) });

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("aspect ratio"));
    }

    [Fact]
    public void Validate_MoreThan30Hashtags_PassesWithWarning()
    {
        var id = Guid.NewGuid();
        var tags = string.Join(" ", Enumerable.Range(1, 31).Select(n => $"#tag{n}"));
        var content = $"Caption {tags}\n![Img](media://{id})";
        var result = _validator.Validate(content, new[] { CreateImage(id) });

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("31 hashtags"));
    }

    [Fact]
    public void Validate_MissingAltText_PassesWithWarning()
    {
        var id = Guid.NewGuid();
        var content = $"Caption\n![](media://{id})";
        var result = _validator.Validate(content, new[] { CreateImage(id, altText: null) });

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("ALT text"));
    }

    [Fact]
    public void Validate_VideoAsset_FailsWithImagesOnlyError()
    {
        var id = Guid.NewGuid();
        var content = $"Video\n![Clip](media://{id})";
        var result = _validator.Validate(content, new[] { CreateImage(id, mimeType: "video/mp4") });

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("images only"));
    }
}
