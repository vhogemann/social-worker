using System;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.LinkedIn;

public sealed class LinkedInMarkdownMapperTests
{
    [Fact]
    public void Map_TextWithBoldAndItalics_CleansAndNormalizes()
    {
        var markdown = "Hello **world** and *everyone*! Check [Google](https://google.com) out.";
        var contract = LinkedInMarkdownMapper.Map(
            content: markdown,
            authorUrn: "urn:li:person:123",
            visibility: "PUBLIC",
            draftId: Guid.Empty,
            threadId: Guid.Empty,
            getMediaAsset: id => null
        );

        Assert.Equal("Hello world and everyone! Check Google out.", contract.BodyText);
        Assert.Equal(LinkedInMediaKind.Article, contract.MediaKind);
        Assert.NotNull(contract.Article);
        Assert.Equal("https://google.com", contract.Article.OriginalUrl);
    }

    [Fact]
    public void Map_SingleImage_ResolvesAndCreatesContract()
    {
        var mediaId = Guid.NewGuid();
        var markdown = $"Check out my new app:\n![App Mockup](media://{mediaId})";
        
        var contract = LinkedInMarkdownMapper.Map(
            content: markdown,
            authorUrn: "urn:li:person:123",
            visibility: "PUBLIC",
            draftId: Guid.Empty,
            threadId: Guid.Empty,
            getMediaAsset: id => id == mediaId ? ("path/to/file.png", "image/png", "Database Alt Text") : null
        );

        Assert.Equal("Check out my new app:", contract.BodyText);
        Assert.Equal(LinkedInMediaKind.Image, contract.MediaKind);
        Assert.NotNull(contract.Image);
        Assert.Equal(mediaId.ToString(), contract.Image.MediaAssetId);
        Assert.Equal("Database Alt Text", contract.Image.AltText);
    }

    [Fact]
    public void Map_YouTubeMarkdown_MapsToArticle()
    {
        var markdown = "Watch our demo: ![Demo Video](https://www.youtube.com/watch?v=12345)";
        
        var contract = LinkedInMarkdownMapper.Map(
            content: markdown,
            authorUrn: "urn:li:person:123",
            visibility: "PUBLIC",
            draftId: Guid.Empty,
            threadId: Guid.Empty,
            getMediaAsset: id => null
        );

        Assert.Equal("Watch our demo:", contract.BodyText);
        Assert.Equal(LinkedInMediaKind.Article, contract.MediaKind);
        Assert.NotNull(contract.Article);
        Assert.Equal("https://www.youtube.com/watch?v=12345", contract.Article.OriginalUrl);
        Assert.Equal("Demo Video", contract.Article.Title);
        Assert.Equal("https://img.youtube.com/vi/12345/hqdefault.jpg", contract.Article.ThumbnailUrl);
    }

    [Fact]
    public void Map_MultipleSegments_ThrowsThreadNotSupported()
    {
        var markdown = "Segment 1\n---\nSegment 2";

        var ex = Assert.Throws<LinkedInContractException>(() =>
            LinkedInMarkdownMapper.Map(
                content: markdown,
                authorUrn: "urn:li:person:123",
                visibility: "PUBLIC",
                draftId: Guid.Empty,
                threadId: Guid.Empty,
                getMediaAsset: id => null
            )
        );

        Assert.Equal(LinkedInContractError.ThreadNotSupportedForLinkedIn, ex.ErrorType);
    }

    [Fact]
    public void Map_MixedMedia_ThrowsUnsupportedCombination()
    {
        var mediaId = Guid.NewGuid();
        var markdown = $"Check [Google](https://google.com) and this ![App](media://{mediaId})";

        var ex = Assert.Throws<LinkedInContractException>(() =>
            LinkedInMarkdownMapper.Map(
                content: markdown,
                authorUrn: "urn:li:person:123",
                visibility: "PUBLIC",
                draftId: Guid.Empty,
                threadId: Guid.Empty,
                getMediaAsset: id => id == mediaId ? ("path", "mime", "alt") : null
            )
        );

        Assert.Equal(LinkedInContractError.UnsupportedCombination, ex.ErrorType);
    }

    [Fact]
    public void Map_UnresolvedAsset_ThrowsMissingMediaAsset()
    {
        var mediaId = Guid.NewGuid();
        var markdown = $"My image: ![App](media://{mediaId})";

        var ex = Assert.Throws<LinkedInContractException>(() =>
            LinkedInMarkdownMapper.Map(
                content: markdown,
                authorUrn: "urn:li:person:123",
                visibility: "PUBLIC",
                draftId: Guid.Empty,
                threadId: Guid.Empty,
                getMediaAsset: id => null // simulates missing from DB
            )
        );

        Assert.Equal(LinkedInContractError.MissingMediaAsset, ex.ErrorType);
    }
}
