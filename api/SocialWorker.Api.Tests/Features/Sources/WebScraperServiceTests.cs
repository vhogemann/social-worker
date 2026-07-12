using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources;

public sealed class WebScraperServiceTests
{
    [Fact]
    public async Task ScrapeUrlAsync_EmptyUrl_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var (title, content, isYouTube) = await scraper.ScrapeUrlAsync("");

        Assert.Equal("Empty URL", title);
        Assert.False(isYouTube);
    }

    [Fact]
    public async Task ScrapeUrlAsync_PrependsHttps()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.StartsWith("https://", req.RequestUri?.OriginalString);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><head><title>Test</title></head><body><p>Hello</p></body></html>")
            };
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var (title, content, isYouTube) = await scraper.ScrapeUrlAsync("example.com");

        Assert.Equal("Test", title);
        Assert.Contains("Hello", content);
        Assert.False(isYouTube);
    }

    [Fact]
    public async Task ScrapeUrlAsync_ExtractsTitleAndContent()
    {
        var html = """
            <html>
              <head><title>My Page</title></head>
              <body>
                <h1>Welcome</h1>
                <p>This is the first paragraph.</p>
                <p>This is the second paragraph.</p>
              </body>
            </html>
            """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var (title, content, isYouTube) = await scraper.ScrapeUrlAsync("https://example.com");

        Assert.Equal("My Page", title);
        Assert.Contains("Welcome", content);
        Assert.Contains("This is the first paragraph.", content);
        Assert.False(isYouTube);
    }

    [Fact]
    public async Task ScrapeUrlAsync_RemovesNoiseElements()
    {
        var html = """
            <html>
              <head><title>Clean Page</title></head>
              <body>
                <script>alert('bad')</script>
                <style>.nope{}</style>
                <nav>nav stuff</nav>
                <p>Actual content</p>
              </body>
            </html>
            """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var (_, content, _) = await scraper.ScrapeUrlAsync("https://example.com");

        Assert.Contains("Actual content", content);
        Assert.DoesNotContain("alert", content);
        Assert.DoesNotContain("nav stuff", content);
    }

    [Fact]
    public async Task ScrapeUrlAsync_DetectsYouTubeUrls()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.OriginalString ?? "";
            if (url.Contains("oembed"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"title":"Test Video","author_name":"Creator","author_url":"https://youtube.com/@creator"}""")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><p>not youtube</p></body></html>")
            };
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var (title, _, isYouTube) = await scraper.ScrapeUrlAsync("https://youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.True(isYouTube);
        Assert.Contains("Test Video", title);
    }
}