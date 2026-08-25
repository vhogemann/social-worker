using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Validation;

public sealed class CanonicalDraftValidatorTests
{
    private readonly CanonicalDraftValidator _validator = new();

    [Fact]
    public void Validate_EmptyContent_ReturnsFailed()
    {
        var result = _validator.Validate("", Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains("empty", result.Posts[0].Issues[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AllowsLongFormContent_Exceeding300Characters()
    {
        var longParagraph = new string('A', 850);
        var content = $"# Main Title\n\n{longParagraph}\n\n---\n\n## Section 2\n\n{longParagraph}";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Equal(2, result.Posts.Count);
    }

    [Fact]
    public void Validate_AllowsRichMarkdown_WithoutWarningsOrErrors()
    {
        var content = "# Headline\n\nThis has **bold text**, *italic text*, and `inline code`.\n\n- Item 1\n- Item 2\n\n> A thoughtful quote.";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Empty(result.Posts[0].Issues);
    }

    [Fact]
    public void Validate_PlaceholderLinks_ReturnsError()
    {
        var content = "Read more about this here: [link](https://example.com) and [insert link](https://g1.globo.com)";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("placeholder"));
    }

    [Fact]
    public void Validate_InvalidFileReferenceGuid_ReturnsError()
    {
        var content = "Check this file: [Document](file://not-a-valid-guid)";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Message.Contains("GUID"));
    }

    [Fact]
    public void Validate_ValidFileAndMediaReferences_PassesValidation()
    {
        var fileId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var content = $"Check this document: [Source Document](file://{fileId})\n\n![A descriptive alt text](media://{mediaId})";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
    }

    [Fact]
    public void Validate_MissingMediaAltText_ReturnsWarning()
    {
        var mediaId = Guid.NewGuid();
        var content = $"Here is an image: ![](media://{mediaId})";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.False(result.HasBlockingErrors);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("ALT text"));
    }
}
