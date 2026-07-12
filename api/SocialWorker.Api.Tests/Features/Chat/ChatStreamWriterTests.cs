using System.Text.Json;
using SocialWorker.Api.Features.Chat;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class ChatStreamWriterTests
{
    private readonly ChatStreamWriter _writer = new();

    [Fact]
    public void MessageId_ReturnsFormattedString()
    {
        var result = _writer.MessageId();
        Assert.StartsWith("f:", result);
        Assert.Contains("messageId", result);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void TextDelta_ReturnsFormattedString()
    {
        var result = _writer.TextDelta("Hello world");
        Assert.StartsWith("0:", result);
        Assert.Contains("Hello world", result);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void ToolCall_ReturnsFormattedString()
    {
        var args = """{"key":"value"}""";
        var result = _writer.ToolCall("tc_1", "test_tool", args);
        Assert.StartsWith("9:", result);
        Assert.Contains("tc_1", result);
        Assert.Contains("test_tool", result);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void ToolCall_WithEmptyArgs_ReturnsEmptyObject()
    {
        var result = _writer.ToolCall("tc_1", "test_tool", "");
        Assert.Contains("\"args\":{}", result);
    }

    [Fact]
    public void ToolResult_ReturnsFormattedString()
    {
        var resultObj = new { success = true, data = "ok" };
        var result = _writer.ToolResult("tc_1", resultObj);
        Assert.StartsWith("a:", result);
        Assert.Contains("tc_1", result);
        Assert.Contains("success", result);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void StepFinish_ReturnsFormattedString()
    {
        var result = _writer.StepFinish("stop", false);
        Assert.StartsWith("e:", result);
        Assert.Contains("stop", result);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void StepFinish_IsContinued_SetsFlag()
    {
        var result = _writer.StepFinish("tool-calls", true);
        Assert.Contains("true", result);
    }

    [Fact]
    public void StreamDone_ReturnsFormattedString()
    {
        var result = _writer.StreamDone();
        Assert.StartsWith("d:", result);
        Assert.Contains("finishReason", result);
        Assert.EndsWith("\n", result);
    }
}