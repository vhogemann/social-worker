using System;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.LinkedIn;

public sealed class LinkedInPayloadMapperTests
{
    [Fact]
    public void MapToUgcPostRequest_TextOnly_MapsCorrectly()
    {
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:123",
            bodyText: "Simple text",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.None,
            article: null,
            image: null,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        var request = LinkedInPayloadMapper.MapToUgcPostRequest(contract);

        Assert.Equal("urn:li:person:123", request.Author);
        Assert.Equal("Simple text", request.SpecificContent.ShareContent.ShareCommentary.Text);
        Assert.Equal("NONE", request.SpecificContent.ShareContent.ShareMediaCategory);
        Assert.Null(request.SpecificContent.ShareContent.Media);
        Assert.Equal("PUBLIC", request.Visibility.MemberNetworkVisibility);
    }

    [Fact]
    public void MapToUgcPostRequest_TextOnly_OmitsMediaPropertyInJson()
    {
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:123",
            bodyText: "Simple text",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.None,
            article: null,
            image: null,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        var request = LinkedInPayloadMapper.MapToUgcPostRequest(contract);
        var json = System.Text.Json.JsonSerializer.Serialize(request);

        Assert.DoesNotContain("\"media\"", json);
    }

    [Fact]
    public void Create_NormalizesRawSubToPersonUrn()
    {
        var contract = LinkedInPostContract.Create(
            authorUrn: "12345",
            bodyText: "Simple text",
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
    }

    [Fact]
    public void MapToUgcPostRequest_Article_MapsCorrectly()
    {
        var article = new LinkedInArticlePayload("https://test.com/post", "My Title", "My Description");
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:123",
            bodyText: "Read this",
            visibility: "CONNECTIONS",
            mediaKind: LinkedInMediaKind.Article,
            article: article,
            image: null,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        var request = LinkedInPayloadMapper.MapToUgcPostRequest(contract);

        var shareContent = request.SpecificContent.ShareContent;
        Assert.Equal("ARTICLE", shareContent.ShareMediaCategory);
        Assert.NotNull(shareContent.Media);
        Assert.Single(shareContent.Media);

        var mediaItem = shareContent.Media[0];
        Assert.Equal("READY", mediaItem.Status);
        Assert.Equal("https://test.com/post", mediaItem.OriginalUrl);
        Assert.Equal("My Title", mediaItem.Title?.Text);
        Assert.Equal("My Description", mediaItem.Description?.Text);
        Assert.Equal("CONNECTIONS", request.Visibility.MemberNetworkVisibility);
    }

    [Fact]
    public void MapToUgcPostRequest_Image_MapsCorrectly()
    {
        var image = new LinkedInImagePayload("asset-ref", "Cool alt text");
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:123",
            bodyText: "Nice view",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.Image,
            article: null,
            image: image,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        var request = LinkedInPayloadMapper.MapToUgcPostRequest(contract, "urn:li:digitalmediaAsset:999");

        var shareContent = request.SpecificContent.ShareContent;
        Assert.Equal("IMAGE", shareContent.ShareMediaCategory);
        Assert.NotNull(shareContent.Media);
        Assert.Single(shareContent.Media);

        var mediaItem = shareContent.Media[0];
        Assert.Equal("READY", mediaItem.Status);
        Assert.Equal("urn:li:digitalmediaAsset:999", mediaItem.MediaUrn);
        Assert.Equal("Cool alt text", mediaItem.Description?.Text);
    }

    [Fact]
    public void MapToUgcPostRequest_ImageMissingUrn_ThrowsInvalidOperationException()
    {
        var image = new LinkedInImagePayload("asset-ref", "Cool alt text");
        var contract = LinkedInPostContract.Create(
            authorUrn: "urn:li:person:123",
            bodyText: "Nice view",
            visibility: "PUBLIC",
            mediaKind: LinkedInMediaKind.Image,
            article: null,
            image: image,
            video: null,
            sourceSegmentIndex: 0,
            draftId: Guid.Empty,
            threadId: Guid.Empty
        );

        Assert.Throws<InvalidOperationException>(() =>
            LinkedInPayloadMapper.MapToUgcPostRequest(contract, null)
        );
    }
}
