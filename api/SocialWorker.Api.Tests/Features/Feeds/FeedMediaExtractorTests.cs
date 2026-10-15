using SocialWorker.Api.Features.Feeds;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedMediaExtractorTests
{
    [Fact]
    public void ExtractMedia_NullOrEmptyContent_ReturnsNull()
    {
        var (url1, type1) = FeedMediaExtractor.ExtractMedia(null);
        Assert.Null(url1);
        Assert.Null(type1);

        var (url2, type2) = FeedMediaExtractor.ExtractMedia(string.Empty);
        Assert.Null(url2);
        Assert.Null(type2);

        var (url3, type3) = FeedMediaExtractor.ExtractMedia("Just plain text with no html");
        Assert.Null(url3);
        Assert.Null(type3);
    }

    [Fact]
    public void ExtractMedia_VideoSourceTag_ReturnsVideoUrlAndType()
    {
        var html = @"<p>Caption text</p><video controls="""" poster=""https://cdn.com/thumb.jpg""><source src=""https://cdn.com/reel.mp4?v=1&amp;token=abc"" type=""video/mp4""></video>";
        var (url, type) = FeedMediaExtractor.ExtractMedia(html);

        Assert.Equal("https://cdn.com/reel.mp4?v=1&token=abc", url);
        Assert.Equal("VIDEO", type);
    }

    [Fact]
    public void ExtractMedia_VideoDirectTag_ReturnsVideoUrlAndType()
    {
        var html = @"<div><video src=""https://cdn.com/direct.mp4""></video></div>";
        var (url, type) = FeedMediaExtractor.ExtractMedia(html);

        Assert.Equal("https://cdn.com/direct.mp4", url);
        Assert.Equal("VIDEO", type);
    }

    [Fact]
    public void ExtractMedia_ImgTag_ReturnsImageUrlAndType()
    {
        var html = @"<p>Photo caption</p><img src=""https://cdn.com/photo.jpg?size=large"" alt=""photo"">";
        var (url, type) = FeedMediaExtractor.ExtractMedia(html);

        Assert.Equal("https://cdn.com/photo.jpg?size=large", url);
        Assert.Equal("IMAGE", type);
    }
}
