using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Infrastructure.Search;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class ImageSearchToolTests
{
    private sealed class MockSearchEngine : ISearchEngine
    {
        public List<SearchResult> ImageResults { get; set; } = new();

        public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
        {
            return Task.FromResult(new List<SearchResult>());
        }

        public Task<List<SearchResult>> SearchImagesAsync(string query, CancellationToken ct)
        {
            return Task.FromResult(ImageResults);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Formatted_Image_Results()
    {
        var mockEngine = new MockSearchEngine
        {
            ImageResults = new List<SearchResult>
            {
                new SearchResult("Pineapple 1", "https://example.com/pineapple1.jpg", "A ripe pineapple"),
                new SearchResult("Pineapple 2", "https://example.com/pineapple2.jpg", "Pineapple on table")
            }
        };

        var tool = new ImageSearchTool(mockEngine);
        var response = await tool.ExecuteAsync(new ImageSearchArgs("pineapple"), null, Guid.NewGuid(), CancellationToken.None);

        Assert.Contains("Image search results for: 'pineapple':", response);
        Assert.Contains("- **Pineapple 1**", response);
        Assert.Contains("URL: https://example.com/pineapple1.jpg", response);
        Assert.Contains("Description: A ripe pineapple", response);
        Assert.Contains("- **Pineapple 2**", response);
        Assert.Contains("URL: https://example.com/pineapple2.jpg", response);
    }
}
