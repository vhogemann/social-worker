using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class SourceExtractorTests
{
    private readonly SourceExtractor _extractor = new();

    [Fact]
    public async Task ExtractTextAsync_TxtFile_ReturnsContent()
    {
        var content = "Hello from a text file!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await _extractor.ExtractTextAsync("test.txt", stream);
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ExtractTextAsync_MdFile_ReturnsContent()
    {
        var content = "# Markdown Title\n\nSome **bold** text.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await _extractor.ExtractTextAsync("test.md", stream);
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ExtractTextAsync_UnsupportedExtension_Throws()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _extractor.ExtractTextAsync("test.docx", stream));
    }
}