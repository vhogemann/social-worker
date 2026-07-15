using System;
using SocialWorker.Api.Features.CodeImages;
using SkiaSharp;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class CodeImageRendererTests : IDisposable
{
    private readonly CodeImageRenderer _renderer = new();

    public void Dispose() => _renderer.Dispose();

    [Fact]
    public void Render_ReturnsPngBytes_ForCSharpBlock()
    {
        var block = new CodeBlock("csharp", "var x = 42;\nConsole.WriteLine(x);");
        var bytes = _renderer.Render(block, CodeTheme.Dark);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void Render_ReturnsPngBytes_ForPlainTextBlock()
    {
        var block = new CodeBlock("", "hello world");
        var bytes = _renderer.Render(block, CodeTheme.Dark);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Render_ReturnsPngBytes_ForLightTheme()
    {
        var block = new CodeBlock("python", "def hello():\n    print('hi')");
        var bytes = _renderer.Render(block, CodeTheme.Light);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Render_ProducesValidDimensions_ForMultilineCode()
    {
        var code = string.Join("\n", System.Linq.Enumerable.Repeat("var x = 1;", 20));
        var block = new CodeBlock("csharp", code);
        var bytes = _renderer.Render(block, CodeTheme.Dark);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width >= 460);
        Assert.True(bitmap.Height > 100);
    }

    [Fact]
    public void Render_RespectsMaxWidth()
    {
        var longLine = new string('x', 300);
        var block = new CodeBlock("", longLine);
        var bytes = _renderer.Render(block, CodeTheme.Dark);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width <= 1200);
    }

    [Fact]
    public void Render_HandlesTabCharacters()
    {
        var block = new CodeBlock("go", "func main() {\n\tfmt.Println(\"hi\")\n}");
        var exception = Record.Exception(() => _renderer.Render(block, CodeTheme.Dark));
        Assert.Null(exception);
    }
}
