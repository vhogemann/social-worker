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

        var result = await scraper.ScrapeUrlAsync("");

        Assert.Equal("Empty URL", result.Title);
        Assert.False(result.IsYouTube);
        Assert.False(result.Success);
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

        var result = await scraper.ScrapeUrlAsync("example.com");

        Assert.Equal("Test", result.Title);
        Assert.Contains("Hello", result.Content);
        Assert.False(result.IsYouTube);
        Assert.True(result.Success);
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

        var result = await scraper.ScrapeUrlAsync("https://example.com");

        Assert.Equal("My Page", result.Title);
        Assert.Contains("Welcome", result.Content);
        Assert.Contains("This is the first paragraph.", result.Content);
        Assert.False(result.IsYouTube);
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

        var result = await scraper.ScrapeUrlAsync("https://example.com");

        Assert.Contains("Actual content", result.Content);
        Assert.DoesNotContain("alert", result.Content);
        Assert.DoesNotContain("nav stuff", result.Content);
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

        var result = await scraper.ScrapeUrlAsync("https://youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.True(result.IsYouTube);
        Assert.Contains("Test Video", result.Title);
    }

    [Fact]
    public async Task ScrapeUrlAsync_DetectsYouTubeShortsUrls()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.OriginalString ?? "";
            if (url.Contains("oembed"))
            {
                Assert.Contains("youtube.com%2Fshorts%2F1olibnzyj4k", url);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"title":"Short Video","author_name":"Creator","author_url":"https://youtube.com/channel/UC1234567890"}""")
                };
            }

            if (url.Contains("feeds/videos.xml"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var result = await scraper.ScrapeUrlAsync("https://www.youtube.com/shorts/1olibnzyj4k");

        Assert.True(result.IsYouTube);
        Assert.Equal("https://www.youtube.com/shorts/1olibnzyj4k", result.FinalUrl);
        Assert.Contains("Short Video", result.Title);
    }

    [Fact]
    public async Task ScrapeUrlAsync_DetectsConsentWrappedYouTubeUrl()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.OriginalString ?? "";
            if (url.Contains("oembed"))
            {
                Assert.Contains("youtube.com%2Fshorts%2F1olibnzyj4k", url);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"title":"Short Video","author_name":"Creator","author_url":"https://youtube.com/channel/UC1234567890"}""")
                };
            }

            if (url.Contains("feeds/videos.xml"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var consentUrl = "https://consent.youtube.com/ml?continue=https://www.youtube.com/shorts/1olibnzyj4k?cbrd%3D1&gl=IE&hl=en-GB";
        var result = await scraper.ScrapeUrlAsync(consentUrl);

        Assert.True(result.IsYouTube);
        Assert.Equal("https://www.youtube.com/shorts/1olibnzyj4k?cbrd=1", result.FinalUrl);
        Assert.Contains("Short Video", result.Title);
    }

    [Fact]
    public async Task ScrapeUrlAsync_ReturnsFailure_For_HttpError()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var result = await scraper.ScrapeUrlAsync("https://example.com/missing");

        Assert.False(result.Success);
        Assert.Contains("HTTP 404", result.Error);
    }

    [Fact]
    public async Task ScrapeUrlAsync_UnsupportedMediaType_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler(_ =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 })
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            return res;
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var result = await scraper.ScrapeUrlAsync("https://example.com/document.pdf");

        Assert.False(result.Success);
        Assert.Contains("Unsupported content type", result.Error);
    }

    [Fact]
    public async Task ScrapeUrlAsync_ExceedsMaxSize_ReturnsFailure()
    {
        var largeBytes = new byte[6 * 1024 * 1024];
        var handler = new MockHttpMessageHandler(_ =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(largeBytes)
            };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return res;
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var result = await scraper.ScrapeUrlAsync("https://example.com/huge.html");

        Assert.False(result.Success);
        Assert.Contains("exceeds maximum allowed size", result.Error);
    }

    [Fact]
    public async Task ScrapeUrlAsync_NonUtf8Encoding_DecodesProperly()
    {
        var iso = System.Text.Encoding.GetEncoding("ISO-8859-1");
        var html = "<html><head><title>Café</title></head><body><p>Déjà vu</p></body></html>";
        var bytes = iso.GetBytes(html);

        var handler = new MockHttpMessageHandler(_ =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            res.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/html; charset=ISO-8859-1");
            return res;
        });
        var client = new HttpClient(handler);
        var scraper = new WebScraperService(client);

        var result = await scraper.ScrapeUrlAsync("https://example.com/french.html");

        Assert.True(result.Success);
        Assert.Equal("Café", result.Title);
        Assert.Contains("Déjà vu", result.Content);
    }
}