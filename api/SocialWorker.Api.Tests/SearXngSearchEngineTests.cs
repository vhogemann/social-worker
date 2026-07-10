using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Infrastructure.Search;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class SearXngSearchEngineTests
{
    [Fact]
    public async Task SearchAsync_Parses_Correct_Response()
    {
        var options = Options.Create(new SearchOptions { SearXngBaseUrl = "http://searxng:8080" });
        var responseBody = """
        {
          "results": [
            {
              "title": "SearXNG Project",
              "url": "https://github.com/searxng/searxng",
              "content": "A privacy-respecting metasearch engine"
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("http://searxng:8080/search?q=my%20query&format=json", req.RequestUri?.OriginalString);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var client = new HttpClient(handler);
        var engine = new SearXngSearchEngine(client, options);

        var results = await engine.SearchAsync("my query", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("SearXNG Project", results[0].Title);
        Assert.Equal("https://github.com/searxng/searxng", results[0].Url);
        Assert.Equal("A privacy-respecting metasearch engine", results[0].Snippet);
    }

    [Fact]
    public async Task SearchImagesAsync_Parses_Correct_Response()
    {
        var options = Options.Create(new SearchOptions { SearXngBaseUrl = "http://searxng:8080" });
        var responseBody = """
        {
          "results": [
            {
              "title": "Pineapple Image",
              "url": "https://example.com/pineapple-page",
              "img_src": "https://images.unsplash.com/photo-12345",
              "content": "A beautiful ripe pineapple"
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("http://searxng:8080/search?q=pineapple&format=json&categories=images", req.RequestUri?.OriginalString);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var client = new HttpClient(handler);
        var engine = new SearXngSearchEngine(client, options);

        var results = await engine.SearchImagesAsync("pineapple", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Pineapple Image", results[0].Title);
        Assert.Equal("https://images.unsplash.com/photo-12345", results[0].Url);
        Assert.Equal("A beautiful ripe pineapple", results[0].Snippet);
    }
}
