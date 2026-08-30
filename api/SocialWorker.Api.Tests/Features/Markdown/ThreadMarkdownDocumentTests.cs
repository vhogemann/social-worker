using System;
using System.Linq;
using SocialWorker.Api.Infrastructure.Markdown;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Markdown;

public sealed class ThreadMarkdownDocumentTests
{
    [Fact]
    public void Parse_WhenEmpty_ReturnsSingleEmptySegment()
    {
        var doc = ThreadMarkdownDocument.Parse("");
        Assert.Single(doc.Segments);
        Assert.Equal(string.Empty, doc.Segments[0].PlainText);
        Assert.Equal(0, doc.Segments[0].CharacterCount);
    }

    [Fact]
    public void Parse_FencedCodeBlockWithDashes_DoesNotSplitSegment()
    {
        var markdown = "Check out this YAML snippet:\n```yaml\nfoo: bar\n---\nbaz: qux\n```\nIt works cleanly!";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Single(doc.Segments);
        Assert.Contains("foo: bar", doc.Segments[0].PlainText);
        Assert.Contains("---", doc.Segments[0].PlainText);
    }

    [Fact]
    public void Parse_BoldInsideInlineCode_DoesNotFlagUnsupportedMarkdown()
    {
        var markdown = "You can run `**test**` command.";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Single(doc.Segments);
        Assert.False(doc.Segments[0].HasUnsupportedFormatting);
        Assert.Empty(doc.Segments[0].UnsupportedFormattingDetails);
    }

    [Fact]
    public void Parse_BoldInBodyText_FlagsUnsupportedMarkdown()
    {
        var markdown = "This is **bold** text and *italic* text.";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Single(doc.Segments);
        Assert.True(doc.Segments[0].HasUnsupportedFormatting);
        Assert.Contains(doc.Segments[0].UnsupportedFormattingDetails, d => d.Contains("Bold", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doc.Segments[0].UnsupportedFormattingDetails, d => d.Contains("Italic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_YouTubeEmbedWithParenthesesInAltText_ExtractsCorrectly()
    {
        var markdown = "Watch this review: ![Joker (2019) Review](https://www.youtube.com/watch?v=48byvBvZ3RU)";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Single(doc.Segments);
        var seg = doc.Segments[0];
        Assert.NotNull(seg.YouTubeEmbed);
        Assert.Equal("https://www.youtube.com/watch?v=48byvBvZ3RU", seg.YouTubeEmbed!.Url);
        Assert.Equal("Joker (2019) Review", seg.YouTubeEmbed.Title);
        Assert.Equal("Watch this review:", seg.PlainText);
        Assert.Equal("Watch this review:".Length, seg.CharacterCount);
    }

    [Fact]
    public void Parse_YouTubeShortsAndLiveStreams_ExtractsEmbeds()
    {
        var markdown = "Shorts: ![Short Video](https://www.youtube.com/shorts/alElfDsk9G8)\n---\nLive: ![Live Stream](https://www.youtube.com/live/12345)";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Equal(2, doc.Segments.Count);
        Assert.NotNull(doc.Segments[0].YouTubeEmbed);
        Assert.Equal("https://www.youtube.com/shorts/alElfDsk9G8", doc.Segments[0].YouTubeEmbed!.Url);

        Assert.NotNull(doc.Segments[1].YouTubeEmbed);
        Assert.Equal("https://www.youtube.com/live/12345", doc.Segments[1].YouTubeEmbed!.Url);
    }

    [Fact]
    public void Parse_MediaEmbeds_ExtractsGuidsAndAlts()
    {
        var mediaId = Guid.NewGuid();
        var markdown = $"Look at this image: ![Architecture Diagram](media://{mediaId})";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Single(doc.Segments);
        var seg = doc.Segments[0];
        Assert.Single(seg.MediaEmbeds);
        Assert.Equal(mediaId, seg.MediaEmbeds[0].MediaId);
        Assert.Equal("Architecture Diagram", seg.MediaEmbeds[0].AltText);
        Assert.Equal("Look at this image:", seg.PlainText);
    }

    [Fact]
    public void Parse_TextLinksAndHashtags_ComputesExactUtf8ByteOffsets()
    {
        var markdown = "Hello 🚀 [Bluesky](https://bsky.app) is great! #social #tech";
        var doc = ThreadMarkdownDocument.Parse(markdown);

        Assert.Single(doc.Segments);
        var seg = doc.Segments[0];

        Assert.Single(seg.TextLinks);
        var link = seg.TextLinks[0];
        Assert.Equal("Bluesky", link.Text);
        Assert.Equal("https://bsky.app", link.Url);

        var utf8 = System.Text.Encoding.UTF8.GetBytes(seg.PlainText);
        var linkSlice = System.Text.Encoding.UTF8.GetString(utf8, link.PlainTextByteStart, link.PlainTextByteEnd - link.PlainTextByteStart);
        Assert.Equal("Bluesky", linkSlice);

        Assert.Equal(2, seg.Hashtags.Count);
        var tag1 = seg.Hashtags[0];
        var tag1Slice = System.Text.Encoding.UTF8.GetString(utf8, tag1.PlainTextByteStart, tag1.PlainTextByteEnd - tag1.PlainTextByteStart);
        Assert.Equal("#social", tag1Slice);
    }
}
