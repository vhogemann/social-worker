using System.Collections.Generic;
using System.Text.Json;
using SocialWorker.Api.Features.Chat.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class TextToolCallParserTests
{
    [Fact]
    public void ContainsToolCallSyntax_DetectsVariousTags()
    {
        Assert.True(TextToolCallParser.ContainsToolCallSyntax("<tool_call>web_search<arg_key>query</arg_key><arg_value>test</arg_value></tool_call>"));
        Assert.True(TextToolCallParser.ContainsToolCallSyntax("Text before <tool_call> something"));
        Assert.True(TextToolCallParser.ContainsToolCallSyntax("</tool_call>"));
        Assert.True(TextToolCallParser.ContainsToolCallSyntax("<arg_key>query</arg_key>"));
        Assert.False(TextToolCallParser.ContainsToolCallSyntax("Normal response without any tags."));
        Assert.False(TextToolCallParser.ContainsToolCallSyntax(null));
        Assert.False(TextToolCallParser.ContainsToolCallSyntax("   "));
    }

    [Fact]
    public void ParseToolCalls_HermesXmlFormat_ParsesCorrectly()
    {
        var text = "<tool_call>web_search<arg_key>query</arg_key><arg_value>PEC do 6x1 Senado votação</arg_value></tool_call>";
        var calls = TextToolCallParser.ParseToolCalls(text);

        Assert.Single(calls);
        Assert.Equal("web_search", calls[0].Function.Name);

        using var doc = JsonDocument.Parse(calls[0].Function.Arguments);
        Assert.Equal("PEC do 6x1 Senado votação", doc.RootElement.GetProperty("query").GetString());
    }

    [Fact]
    public void ParseToolCalls_MultipleHermesToolCalls_ParsesAll()
    {
        var text = "<tool_call>web_search<arg_key>query</arg_key><arg_value>PEC do 6x1</arg_value></tool_call><tool_call>web_search<arg_key>query</arg_key><arg_value>Queiroz</arg_value></tool_call>";
        var calls = TextToolCallParser.ParseToolCalls(text);

        Assert.Equal(2, calls.Count);
        Assert.Equal("web_search", calls[0].Function.Name);
        Assert.Equal("web_search", calls[1].Function.Name);

        using var doc1 = JsonDocument.Parse(calls[0].Function.Arguments);
        Assert.Equal("PEC do 6x1", doc1.RootElement.GetProperty("query").GetString());

        using var doc2 = JsonDocument.Parse(calls[1].Function.Arguments);
        Assert.Equal("Queiroz", doc2.RootElement.GetProperty("query").GetString());
    }

    [Fact]
    public void ParseToolCalls_JsonInsideToolCall_ParsesCorrectly()
    {
        var text = """
        <tool_call>
        {"name": "search_sources", "arguments": {"query": "reforma tributária", "limit": 3}}
        </tool_call>
        """;
        var calls = TextToolCallParser.ParseToolCalls(text);

        Assert.Single(calls);
        Assert.Equal("search_sources", calls[0].Function.Name);

        using var doc = JsonDocument.Parse(calls[0].Function.Arguments);
        Assert.Equal("reforma tributária", doc.RootElement.GetProperty("query").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("limit").GetInt32());
    }

    [Fact]
    public void StripToolCalls_RemovesTagsAndLeavesSurroundingText()
    {
        var text = "Prefix text <tool_call>web_search<arg_key>query</arg_key><arg_value>test</arg_value></tool_call> suffix text.";
        var stripped = TextToolCallParser.StripToolCalls(text);

        Assert.Equal("Prefix text  suffix text.", stripped);
    }

    [Fact]
    public void ParseToolCalls_DsmlFormat_ParsesCorrectly()
    {
        var text = """
        <｜DSML｜ calls>
        <｜DSML｜ invoke name="search_sources">
        <｜DSML｜ parameter name="limit" string="false">10</｜DSML｜ parameter>
        <｜DSML｜ parameter name="query" string="true">Tesouraço Flávio Bolsonaro aposentados salário mínimo</｜DSML｜ parameter>
        </｜DSML｜ invoke>
        <｜DSML｜ invoke name="web_search">
        <｜DSML｜ parameter name="query" string="true">"Tesouraço" Flávio Bolsonaro aposentados salário mínimo desindexação</｜DSML｜ parameter>
        </｜DSML｜ invoke>
        </｜DSML｜ calls>
        """;

        Assert.True(TextToolCallParser.ContainsToolCallSyntax(text));

        var calls = TextToolCallParser.ParseToolCalls(text);
        Assert.Equal(2, calls.Count);

        Assert.Equal("search_sources", calls[0].Function.Name);
        using var doc1 = JsonDocument.Parse(calls[0].Function.Arguments);
        Assert.Equal(10, doc1.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal("Tesouraço Flávio Bolsonaro aposentados salário mínimo", doc1.RootElement.GetProperty("query").GetString());

        Assert.Equal("web_search", calls[1].Function.Name);
        using var doc2 = JsonDocument.Parse(calls[1].Function.Arguments);
        Assert.Equal("\"Tesouraço\" Flávio Bolsonaro aposentados salário mínimo desindexação", doc2.RootElement.GetProperty("query").GetString());

        var stripped = TextToolCallParser.StripToolCalls(text);
        Assert.Empty(stripped);
    }
}
