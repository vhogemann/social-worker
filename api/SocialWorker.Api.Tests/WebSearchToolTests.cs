using System;
using System.Collections.Generic;
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
            new("Result 2", "https://example.com/2", "This is description 2")
        };
        var engine = new MockSearchEngine { ExpectedQuery = "real query", ResultsToReturn = results };
        var tool = new WebSearchTool(engine);

        var result = await tool.ExecuteAsync(new WebSearchArgs("real query"), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Contains("Web search results for: 'real query':", result);
        Assert.Contains("- **Result 1**", result);
        Assert.Contains("URL: https://example.com/1", result);
        Assert.Contains("Snippet: This is description 1", result);
        Assert.Contains("- **Result 2**", result);
    }
}
