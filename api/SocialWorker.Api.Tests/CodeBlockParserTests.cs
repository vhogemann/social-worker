using System.Collections.Generic;
using SocialWorker.Api.Features.CodeImages;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class CodeBlockParserTests
{
    [Fact]
    public void Parse_ReturnsEmpty_WhenNoFences()
    {
        var result = CodeBlockParser.Parse("Just some text with no code.");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ReturnsSingleBlock_WithLanguage()
    {
        var md = "Some text\n```csharp\nvar x = 1;\n```\nMore text";
        var result = CodeBlockParser.Parse(md);
        Assert.Single(result);
        Assert.Equal("csharp", result[0].Language);
        Assert.Equal("var x = 1;", result[0].Code);
    }

    [Fact]
    public void Parse_ReturnsSingleBlock_WithNoLanguage()
    {
        var md = "```\nplain text\n```";
        var result = CodeBlockParser.Parse(md);
        Assert.Single(result);
        Assert.Equal("", result[0].Language);
        Assert.Equal("plain text", result[0].Code);
    }

    [Fact]
    public void Parse_ReturnsMultipleBlocks()
    {
        var md = "```js\nconsole.log('hi');\n```\nSome prose\n```python\nprint('hello')\n```";
        var result = CodeBlockParser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.Equal("js", result[0].Language);
        Assert.Equal("python", result[1].Language);
    }

    [Fact]
    public void Parse_TrimsTrailingNewlines_FromCode()
    {
        var md = "```go\nfmt.Println(\"hi\")\n\n```";
        var result = CodeBlockParser.Parse(md);
        Assert.Single(result);
        Assert.Equal("fmt.Println(\"hi\")", result[0].Code);
    }

    [Fact]
    public void Parse_HandlesMultilineCode()
    {
        var md = "```csharp\nclass Foo\n{\n    void Bar() { }\n}\n```";
        var result = CodeBlockParser.Parse(md);
        Assert.Single(result);
        Assert.Contains("class Foo", result[0].Code);
        Assert.Contains("void Bar()", result[0].Code);
    }
}
