using System;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.Feeds;

public static class FeedMediaExtractor
{
    private static readonly Regex VideoSourceRegex = new("""<source[^>]+src=["']([^"']+)["'][^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VideoTagRegex = new("""<video[^>]+src=["']([^"']+)["'][^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImageTagRegex = new("""<img[^>]+src=["']([^"']+)["'][^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (string? MediaUrl, string? MediaType) ExtractMedia(string? htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return (null, null);

        var videoSourceMatch = VideoSourceRegex.Match(htmlContent);
        if (videoSourceMatch.Success && !string.IsNullOrWhiteSpace(videoSourceMatch.Groups[1].Value))
        {
            return (System.Net.WebUtility.HtmlDecode(videoSourceMatch.Groups[1].Value), "VIDEO");
        }

        var videoTagMatch = VideoTagRegex.Match(htmlContent);
        if (videoTagMatch.Success && !string.IsNullOrWhiteSpace(videoTagMatch.Groups[1].Value))
        {
            return (System.Net.WebUtility.HtmlDecode(videoTagMatch.Groups[1].Value), "VIDEO");
        }

        var imgMatch = ImageTagRegex.Match(htmlContent);
        if (imgMatch.Success && !string.IsNullOrWhiteSpace(imgMatch.Groups[1].Value))
        {
            return (System.Net.WebUtility.HtmlDecode(imgMatch.Groups[1].Value), "IMAGE");
        }

        return (null, null);
    }
}
