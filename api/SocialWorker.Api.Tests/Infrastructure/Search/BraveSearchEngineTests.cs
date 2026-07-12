using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Infrastructure.Search;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFunc)
    {
        _responseFunc = responseFunc;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseFunc(request));
    }
}

public sealed class BraveSearchEngineTests
{
    [Fact]
    public async Task SearchAsync_Throws_If_ApiKey_Empty()
    {
        var options = Options.Create(new SearchOptions { BraveApiKey = "" });
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler);
        var engine = new BraveSearchEngine(client, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SearchAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_Parses_Correct_Response()
    {
        var options = Options.Create(new SearchOptions { BraveApiKey = "test-token" });
        var responseBody = """
        {
          "web": {
            "results": [
              {
                "title": "Brave Search",
                "url": "https://search.brave.com",
                "description": "Privacy-respecting search engine"
              }
            ]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("https://api.search.brave.com/res/v1/web/search?q=test%20query", req.RequestUri?.OriginalString);
            
            var enumerator = req.Headers.GetValues("X-Subscription-Token").GetEnumerator();
            var token = enumerator.MoveNext() ? enumerator.Current : "";
            Assert.Equal("test-token", token);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var client = new HttpClient(handler);
        var engine = new BraveSearchEngine(client, options);

        var results = await engine.SearchAsync("test query", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Brave Search", results[0].Title);
        Assert.Equal("https://search.brave.com", results[0].Url);
        Assert.Equal("Privacy-respecting search engine", results[0].Snippet);
    }
}
