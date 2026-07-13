using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Infrastructure.Search;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class MockSearchEngine : ISearchEngine
{
    public string ExpectedQuery { get; set; } = "";
    public List<SearchResult> ResultsToReturn { get; set; } = new();

    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        Assert.Equal(ExpectedQuery, query);
        return Task.FromResult(ResultsToReturn);
    }
}

public sealed class WebSearchToolTests
{
    [Fact]
    public async Task ExecuteAsync_Returns_NoResults_Message_If_Empty()
    {
        var engine = new MockSearchEngine { ExpectedQuery = "empty test", ResultsToReturn = new() };
        var tool = new WebSearchTool(engine);

        var result = await tool.ExecuteAsync(new WebSearchArgs("empty test"), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("No search results found.", result);
    }

    [Fact]
    public async Task ExecuteAsync_Formats_Results_Correctly()
    {
        var results = new List<SearchResult>
        {
            new("Result 1", "https://example.com/1", "This is description 1"),
            new("Result 2", "https://example.com/2", "This is description 2"),
            new("Bad Result", "/relative/path", "Should be filtered")
        };
        var engine = new MockSearchEngine { ExpectedQuery = "real query", ResultsToReturn = results };
        var tool = new WebSearchTool(engine);

        var result = await tool.ExecuteAsync(new WebSearchArgs("real query"), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        Assert.Equal("real query", root.GetProperty("query").GetString());
        Assert.Equal(2, root.GetProperty("results").GetArrayLength());
        Assert.Equal("https://example.com/1", root.GetProperty("results")[0].GetProperty("url").GetString());
        Assert.Equal("https://example.com/2", root.GetProperty("results")[1].GetProperty("url").GetString());
    }
}
