using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace SocialWorker.Api.Features.Sources;

public sealed class WebScraperService
{
    private readonly HttpClient _client;

    public WebScraperService(HttpClient client)
    {
        _client = client;
    }

    public async Task<(string Title, string Content, bool IsYouTube)> ScrapeUrlAsync(string url)
    {
        if (IsYouTubeUrl(url))
        {
            var (title, contentText) = await FetchYouTubeMetadataAsync(url);
            return (title, contentText, true);
        }
        else
        {
            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            var html = await _client.GetStringAsync(url);
            var (title, text) = ExtractUrlContent(html, url);
            return (title, text, false);
        }
    }

    private static bool IsYouTubeUrl(string url)
    {
        return url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string Title, string Content)> FetchYouTubeMetadataAsync(string url)
    {
        _client.DefaultRequestHeaders.UserAgent.Clear();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        string title = url;
        string authorName = "";
        string authorUrl = "";
        string description = "";
        string views = "unknown";
        string publishedDate = "unknown";

        try
        {
            var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
            var oEmbedRes = await _client.GetAsync(oEmbedUrl);
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
        catch
        {
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
            catch
            {
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

        var noiseNodes = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//iframe|//noscript");
        if (noiseNodes != null)
        {
            foreach (var node in noiseNodes)
            {
                node.Remove();
            }
        }

        var textNodes = doc.DocumentNode.SelectNodes("//p|//h1|//h2|//h3|//h4|//li");
        if (textNodes == null)
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
