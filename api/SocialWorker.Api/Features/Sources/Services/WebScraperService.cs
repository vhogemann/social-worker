using System;
using System.IO;
using System.Net.Http;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace SocialWorker.Api.Features.Sources;

public sealed record WebScrapeResult(
    string RequestedUrl,
    string FinalUrl,
    string Title,
    string Content,
    bool IsYouTube,
    bool HasUsableContent,
    string? Error)
{
    public bool Success => string.IsNullOrWhiteSpace(Error);
}

public sealed class WebScraperService
{
    private const int MaxResponseSizeBytes = 5 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly ILogger<WebScraperService>? _logger;

    public WebScraperService(HttpClient client, ILogger<WebScraperService>? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<WebScrapeResult> ScrapeUrlAsync(string url)
    {
        var requestedUrl = url?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(url))
        {
            return new WebScrapeResult(requestedUrl, requestedUrl, "Empty URL", "", false, false, "URL is empty.");
        }

        if (!TryNormalizeUrl(url, out var normalizedUrl, out var error))
        {
            return new WebScrapeResult(requestedUrl, requestedUrl, requestedUrl, "", false, false, error);
        }

        normalizedUrl = UnwrapYouTubeConsentUrl(normalizedUrl);

        if (IsYouTubeUrl(normalizedUrl))
        {
            var (title, contentText) = await FetchYouTubeMetadataAsync(normalizedUrl);
            return new WebScrapeResult(
                requestedUrl,
                normalizedUrl,
                title,
                contentText,
                true,
                !string.IsNullOrWhiteSpace(contentText),
                null);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return new WebScrapeResult(
                    requestedUrl,
                    normalizedUrl,
                    normalizedUrl,
                    "",
                    false,
                    false,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }


            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "text/html";
            if (!IsSupportedMediaType(mediaType))
            {
                return new WebScrapeResult(
                    requestedUrl,
                    normalizedUrl,
                    normalizedUrl,
                    "",
                    false,
                    false,
                    $"Unsupported content type '{mediaType}'. Expected text or HTML.");
            }

            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaxResponseSizeBytes)
            {
                return new WebScrapeResult(
                    requestedUrl,
                    normalizedUrl,
                    normalizedUrl,
                    "",
                    false,
                    false,
                    $"Response exceeds maximum allowed size of {MaxResponseSizeBytes / (1024 * 1024)} MB.");
            }

            var encoding = GetEncoding(response.Content.Headers.ContentType?.CharSet);
            var (readSuccess, html, readError) = await ReadBoundedContentAsync(response.Content, encoding);
            if (!readSuccess)
            {
                return new WebScrapeResult(
                    requestedUrl,
                    normalizedUrl,
                    normalizedUrl,
                    "",
                    false,
                    false,
                    readError ?? "Failed to read response content.");
            }

            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? normalizedUrl;
            var (title, text) = ExtractUrlContent(html, finalUrl);
            var hasUsableContent = !string.IsNullOrWhiteSpace(text);

            return new WebScrapeResult(
                requestedUrl,
                finalUrl,
                title,
                text,
                false,
                hasUsableContent,
                hasUsableContent ? null : "The page was fetched but no usable text content was extracted.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to scrape URL {Url}", requestedUrl);
            return new WebScrapeResult(requestedUrl, normalizedUrl, normalizedUrl, "", false, false, ex.Message);
        }
    }

    private static bool TryNormalizeUrl(string input, out string normalizedUrl, out string error)
    {
        normalizedUrl = input.Trim();
        error = "";

        if (!normalizedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = "https://" + normalizedUrl;
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            error = "The URL could not be parsed as an absolute HTTP or HTTPS URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Only HTTP and HTTPS URLs are supported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "The URL must include a valid host.";
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    private static bool IsYouTubeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/'));
        }

        if (!uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("watch", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("live/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HttpUtility.ParseQueryString(uri.Query).Get("v") is { Length: > 0 };
    }

    private static string UnwrapYouTubeConsentUrl(string normalizedUrl)
    {
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return normalizedUrl;
        }

        if (!uri.Host.Contains("consent.youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedUrl;
        }

        var continueParam = HttpUtility.ParseQueryString(uri.Query).Get("continue");
        if (string.IsNullOrWhiteSpace(continueParam))
        {
            return normalizedUrl;
        }

        var unescaped = Uri.UnescapeDataString(continueParam);
        if (!Uri.TryCreate(unescaped, UriKind.Absolute, out var continueUri))
        {
            return normalizedUrl;
        }

        if (!continueUri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) &&
            !continueUri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedUrl;
        }

        return continueUri.ToString();
    }

    private async Task<(string Title, string Content)> FetchYouTubeMetadataAsync(string url)
    {
        string title = url;
        string authorName = "";
        string authorUrl = "";
        string description = "";
        string views = "unknown";
        string publishedDate = "unknown";

        try
        {
            var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
            using var oEmbedReq = new HttpRequestMessage(HttpMethod.Get, oEmbedUrl);
            oEmbedReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var oEmbedRes = await _client.SendAsync(oEmbedReq);
            if (oEmbedRes.IsSuccessStatusCode)
            {
                var json = await oEmbedRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("title", out var titleProp)) title = titleProp.GetString() ?? url;
                if (root.TryGetProperty("author_name", out var authorProp)) authorName = authorProp.GetString() ?? "";
                if (root.TryGetProperty("author_url", out var authorUrlProp)) authorUrl = authorUrlProp.GetString() ?? "";
            }
        }

        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch oEmbed metadata for {Url}", url);
        }

        if (!string.IsNullOrEmpty(authorUrl))
        {
            try
            {
                string? channelId = null;
                if (authorUrl.Contains("/channel/UC"))
                {
                    var index = authorUrl.IndexOf("/channel/");
                    if (index >= 0)
                    {
                        channelId = authorUrl.Substring(index + 9);
                    }
                }
                else if (authorUrl.Contains("/@"))
                {
                    var channelHtml = await _client.GetStringAsync(authorUrl);
                    var match = Regex.Match(channelHtml, @"youtube\.com/feeds/videos\.xml\?channel_id=(UC[\w-]+)");
                    if (match.Success)
                    {
                        channelId = match.Groups[1].Value;
                    }
                }

                if (!string.IsNullOrEmpty(channelId))
                {
                    var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
                    var feedXml = await _client.GetStringAsync(feedUrl);

                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.LoadXml(feedXml);

                    var nsmgr = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
                    nsmgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
                    nsmgr.AddNamespace("yt", "http://www.youtube.com/xml/schemas/2015");
                    nsmgr.AddNamespace("media", "http://search.yahoo.com/mrss/");

                    var videoId = ExtractVideoId(url);
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        var entryNode = xmlDoc.SelectSingleNode($"//atom:entry[yt:videoId='{videoId}']", nsmgr);
                        if (entryNode != null)
                        {
                            description = entryNode.SelectSingleNode("media:group/media:description", nsmgr)?.InnerText ?? "";
                            views = entryNode.SelectSingleNode("media:group/media:community/media:statistics/@views", nsmgr)?.Value ?? "unknown";
                            publishedDate = entryNode.SelectSingleNode("atom:published", nsmgr)?.InnerText ?? "unknown";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to fetch YouTube RSS feed for {AuthorUrl}", authorUrl);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        if (!string.IsNullOrEmpty(authorName))
        {
            sb.AppendLine($"By {authorName}");
        }
        if (views != "unknown" || publishedDate != "unknown")
        {
            sb.AppendLine($"Stats: {views} views | Published {publishedDate}");
        }
        sb.AppendLine();
        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
        }
        else
        {
            sb.AppendLine("(No description available)");
        }

        return (title, sb.ToString().Trim());
    }

    private static bool IsSupportedMediaType(string mediaType)
    {
        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mediaType is "application/xhtml+xml"
            or "application/xml"
            or "application/rss+xml"
            or "application/atom+xml"
            or "application/json";
    }

    private static Encoding GetEncoding(string? charSet)
    {
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                return Encoding.GetEncoding(charSet.Trim('\"', ' '));
            }
            catch
            {
            }
        }
        return Encoding.UTF8;
    }

    private static async Task<(bool Success, string Content, string? Error)> ReadBoundedContentAsync(HttpContent content, Encoding encoding)
    {
        await using var stream = await content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxResponseSizeBytes)
            {
                return (false, string.Empty, $"Response exceeds maximum allowed size of {MaxResponseSizeBytes / (1024 * 1024)} MB.");
            }
            ms.Write(buffer, 0, bytesRead);
        }

        ms.Position = 0;
        using var reader = new StreamReader(ms, encoding, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        return (true, text, null);
    }

    private static string? ExtractVideoId(string url)
    {
        if (url.Contains("youtube.com/watch"))
        {
            var match = Regex.Match(url, @"[?&]v=([\w-]+)");
            if (match.Success) return match.Groups[1].Value;
        }
        else if (url.Contains("youtu.be/"))
        {
            var uri = new Uri(url);
            return uri.AbsolutePath.TrimStart('/');
        }
        return null;
    }

    private static (string Title, string Text) ExtractUrlContent(string html, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? url;

        IEnumerable<HtmlNode> noiseNodes = doc.DocumentNode
            .SelectNodes("//script|//style|//nav|//header|//footer|//iframe|//noscript")
            ?.Cast<HtmlNode>()
            ?? Array.Empty<HtmlNode>();
        foreach (var node in noiseNodes)
        {
            node.Remove();
        }

        IEnumerable<HtmlNode> textNodes = doc.DocumentNode
            .SelectNodes("//p|//h1|//h2|//h3|//h4|//li")
            ?.Cast<HtmlNode>()
            ?? Array.Empty<HtmlNode>();
        if (!textNodes.Any())
        {
            return (title, doc.DocumentNode.InnerText?.Trim() ?? "");
        }

        var sb = new StringBuilder();
        foreach (var node in textNodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText?.Trim() ?? "");
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (node.Name.StartsWith("h"))
                {
                    sb.AppendLine().AppendLine($"# {text}");
                }
                else if (node.Name == "li")
                {
                    sb.AppendLine($"- {text}");
                }
                else
                {
                    sb.AppendLine().AppendLine(text);
                }
            }
        }

        return (title, sb.ToString().Trim());
    }
}
