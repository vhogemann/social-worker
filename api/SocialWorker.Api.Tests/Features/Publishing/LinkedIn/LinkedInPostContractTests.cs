using System;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.LinkedIn;

public sealed class LinkedInPostContractTests
{
    [Fact]
    public void Create_ValidTextOnly_ReturnsContract()
    {
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:12345",
            bodyText: "Hello LinkedIn!",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.None,
            article: null,
            image: null,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        Assert.Equal("urn:li:person:12345", contract.AuthorUrn);
        Assert.Equal("Hello LinkedIn!", contract.BodyText);
        Assert.Equal(LinkedInMediaKind.None, contract.MediaKind);
        Assert.Null(contract.Article);
        Assert.Null(contract.Image);
    }

    [Fact]
    public void Create_ValidArticle_ReturnsContract()
    {
        var articlePayload = new LinkedInArticlePayload("https://example.com", "Title", "Desc");
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:12345",
            bodyText: "Check this out!",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.Article,
            article: articlePayload,
            image: null,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        Assert.Equal(LinkedInMediaKind.Article, contract.MediaKind);
        Assert.NotNull(contract.Article);
        Assert.Equal("https://example.com", contract.Article.OriginalUrl);
    }

    [Fact]
    public void Create_ValidImage_ReturnsContract()
    {
        var imagePayload = new LinkedInImagePayload("media-id-123", "Alt Text");
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:12345",
            bodyText: "An image post",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.Image,
            article: null,
            image: imagePayload,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        Assert.Equal(LinkedInMediaKind.Image, contract.MediaKind);
        Assert.NotNull(contract.Image);
        Assert.Equal("media-id-123", contract.Image.MediaAssetId);
    }

    [Fact]
    public void Create_TextTooLong_ThrowsInvalidSegmentLength()
    {
        var longText = new string('x', 3001);

        var ex = Assert.Throws<LinkedInContractException>(() =>
            LinkedInPostContract.Create(
                authorUrn: "urn:li:person:12345",
                bodyText: longText,
                visibility: "PUBLIC",
                mediaKind: LinkedInMediaKind.None,
                article: null,
                image: null,
                video: null,
                sourceSegmentIndex: 0,
                draftId: Guid.Empty,
                threadId: Guid.Empty
            )
        );

        Assert.Equal(LinkedInContractError.InvalidSegmentLength, ex.ErrorType);
    }

    [Fact]
    public void Create_VideoMedia_ThrowsUnsupportedMvpFeature()
    {
        var videoPayload = new LinkedInVideoPayload("media-id-123", "Title", "Desc");

        var ex = Assert.Throws<LinkedInContractException>(() =>
            LinkedInPostContract.Create(
                authorUrn: "urn:li:person:12345",
                bodyText: "Some text",
                visibility: "PUBLIC",
                mediaKind: LinkedInMediaKind.VideoFuture,
                article: null,
                image: null,
                video: videoPayload,
                sourceSegmentIndex: 0,
                draftId: Guid.Empty,
                threadId: Guid.Empty
            )
        );

        Assert.Equal(LinkedInContractError.UnsupportedMvpFeature, ex.ErrorType);
    }

    [Fact]
    public void Create_MixedMedia_ThrowsUnsupportedCombination()
    {
        var articlePayload = new LinkedInArticlePayload("https://example.com");
        var imagePayload = new LinkedInImagePayload("media-id-123");

        var ex = Assert.Throws<LinkedInContractException>(() =>
            LinkedInPostContract.Create(
                authorUrn: "urn:li:person:12345",
                bodyText: "Some text",
                visibility: "PUBLIC",
                mediaKind: LinkedInMediaKind.Image,
                article: articlePayload,
                image: imagePayload,
                video: null,
                sourceSegmentIndex: 0,
                draftId: Guid.Empty,
                threadId: Guid.Empty
            )
        );

        Assert.Equal(LinkedInContractError.UnsupportedCombination, ex.ErrorType);
    }
}
