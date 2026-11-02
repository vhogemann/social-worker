using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Infrastructure.Services;

namespace SocialWorker.Api.Features.Drafts.Services;

public static class DraftPreviewHtmlRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .Build();

    private static readonly Regex MediaEmbedRegex = new(
        @"!\[(?<alt>[^\]]*)\]\(media:\/\/(?<id>[a-fA-F0-9\-]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex BareMediaRegex = new(
        @"media:\/\/(?<id>[a-fA-F0-9\-]+)",
        RegexOptions.Compiled);

    private static readonly Regex YouTubeEmbedRegex = new(
        @"!\[(?<alt>[^\]]*)\]\((?<url>https?:\/\/[^\s\)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex YouTubeUrlRegex = new(
        @"https?:\/\/(?:www\.)?(?:youtube\.com\/(?:watch\?[^\s<""]*v=|shorts\/|live\/|embed\/)|youtu\.be\/)([a-zA-Z0-9_\-]{11})[^\s<""]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Render(Draft draft, string? platform, IAppUrlResolver urlResolver)
    {
        var targetPlatform = !string.IsNullOrWhiteSpace(platform)
            ? platform
            : draft.TargetPlatform?.ToString() ?? "Bluesky";

        var platformThread = draft.Threads?.FirstOrDefault(t => string.Equals(t.Platform, targetPlatform, StringComparison.OrdinalIgnoreCase))
            ?? draft.Threads?.FirstOrDefault();

        var rawContent = !string.IsNullOrWhiteSpace(platformThread?.Content)
            ? platformThread.Content
            : !string.IsNullOrWhiteSpace(draft.Content)
                ? draft.Content
                : draft.Title ?? string.Empty;

        var segments = DraftSegmentService.SplitMarkdownIntoSegments(rawContent);
        if (segments.Count == 0 && !string.IsNullOrWhiteSpace(rawContent))
        {
            segments = new List<string> { rawContent };
        }

        var firstSegment = segments.Count > 0 ? CleanPlainTextForMeta(segments[0]) : string.Empty;
        var webDraftUrl = urlResolver.GetDraftWebUrl(draft.Id);
        var title = string.IsNullOrWhiteSpace(draft.Title) ? "Social Worker Draft Preview" : draft.Title;

        var segmentsBuilder = new StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var segmentHtml = RenderSegment(segment, i + 1, Math.Max(1, segments.Count), urlResolver);
            segmentsBuilder.AppendLine(segmentHtml);
        }

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta name=""color-scheme"" content=""dark"">
    <title>{WebUtility.HtmlEncode(title)}</title>
    <meta property=""og:title"" content=""{WebUtility.HtmlEncode(title)}"" />
    <meta property=""og:description"" content=""{WebUtility.HtmlEncode(firstSegment)}"" />
    <meta property=""og:url"" content=""{webDraftUrl}"" />
    <meta name=""twitter:title"" content=""{WebUtility.HtmlEncode(title)}"" />
    <meta name=""twitter:description"" content=""{WebUtility.HtmlEncode(firstSegment)}"" />
    <style>
        :root {{
            color-scheme: dark;
        }}
        * {{
            box-sizing: border-box;
        }}
        body {{
            margin: 0;
            padding: 32px 16px;
            background-color: #0b0e14;
            color: #f0f6fc;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            min-height: 100vh;
        }}
        .preview-card {{
            max-width: 680px;
            width: 100%;
            background-color: #161b22;
            border: 1px solid #30363d;
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 12px 32px rgba(0, 0, 0, 0.5);
        }}
        .header {{
            padding: 24px;
            border-bottom: 1px solid #30363d;
            background-color: #0d1117;
        }}
        .header h1 {{
            margin: 0 0 8px 0;
            font-size: 20px;
            font-weight: 700;
            color: #f0f6fc;
        }}
        .meta {{
            font-size: 13px;
            color: #8b949e;
        }}
        .segments-list {{
            padding: 24px;
            display: flex;
            flex-direction: column;
            gap: 20px;
        }}
        .segment {{
            background-color: #0d1117;
            border: 1px solid #21262d;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
        }}
        .seg-badge {{
            display: inline-block;
            font-size: 12px;
            font-weight: 600;
            text-transform: uppercase;
            color: #58a6ff;
            margin-bottom: 12px;
            letter-spacing: 0.5px;
        }}
        .seg-content {{
            font-size: 16px;
            line-height: 1.65;
            color: #f0f6fc;
            word-break: break-word;
        }}
        .seg-content p {{
            margin: 0 0 12px 0;
            color: #f0f6fc;
        }}
        .seg-content p:last-child {{
            margin-bottom: 0;
        }}
        .seg-content a {{
            color: #58a6ff;
            text-decoration: underline;
            text-underline-offset: 2px;
            word-break: break-all;
        }}
        .seg-content a:hover {{
            color: #79c0ff;
        }}
        .seg-content blockquote {{
            margin: 12px 0;
            padding: 8px 16px;
            border-left: 3px solid #388bfd;
            background-color: rgba(56, 139, 253, 0.1);
            color: #c9d1d9;
            border-radius: 0 8px 8px 0;
        }}
        .embed-video-wrapper {{
            margin-top: 16px;
            border-radius: 12px;
            overflow: hidden;
            border: 1px solid #30363d;
            background-color: #000;
            position: relative;
            width: 100%;
            aspect-ratio: 16 / 9;
        }}
        .embed-video-wrapper iframe {{
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            border: 0;
            display: block;
        }}
        .embed-image-wrapper {{
            margin-top: 16px;
            border-radius: 12px;
            overflow: hidden;
            border: 1px solid #30363d;
            background-color: #010409;
        }}
        .embed-image-wrapper img {{
            display: block;
            width: 100%;
            height: auto;
            max-height: 550px;
            object-fit: contain;
            background-color: #010409;
        }}
        .footer {{
            padding: 16px 24px;
            display: flex;
            justify-content: flex-end;
            border-top: 1px solid #30363d;
            background-color: #0d1117;
        }}
        .btn {{
            background-color: #238636;
            color: #ffffff !important;
            text-decoration: none;
            padding: 10px 20px;
            border-radius: 8px;
            font-weight: 600;
            font-size: 14px;
            transition: background-color 0.2s;
            display: inline-block;
        }}
        .btn:hover {{
            background-color: #2ea043;
        }}
    </style>
</head>
<body>
    <div class=""preview-card"">
        <div class=""header"">
            <h1>{WebUtility.HtmlEncode(title)}</h1>
            <div class=""meta"">Platform: <strong>{WebUtility.HtmlEncode(targetPlatform)}</strong> • Status: <strong>{draft.Status}</strong> • {Math.Max(1, segments.Count)} post{(segments.Count == 1 ? "" : "s")}</div>
        </div>
        <div class=""segments-list"">
{segmentsBuilder}
        </div>
        <div class=""footer"">
            <a class=""btn"" href=""{webDraftUrl}"">Open Draft in social-worker &rarr;</a>
        </div>
    </div>
</body>
</html>";
    }

    private static string RenderSegment(string rawSegment, int index, int total, IAppUrlResolver urlResolver)
    {
        var images = new List<(string Url, string Alt)>();
        string? youtubeVideoId = null;

        // 1. Extract markdown media embeds: ![alt](media://guid)
        var cleaned = MediaEmbedRegex.Replace(rawSegment, match =>
        {
            var alt = match.Groups["alt"].Value;
            if (Guid.TryParse(match.Groups["id"].Value, out var mediaId))
            {
                var resolvedUrl = urlResolver.GetMediaUrl(mediaId);
                images.Add((resolvedUrl, alt));
            }
            return string.Empty;
        });

        // 2. Extract markdown YouTube embeds: ![alt](https://youtube...)
        cleaned = YouTubeEmbedRegex.Replace(cleaned, match =>
        {
            var url = match.Groups["url"].Value;
            var alt = match.Groups["alt"].Value;
            var vId = LinkedInMarkdownMapper.TryExtractYouTubeVideoId(url);
            if (!string.IsNullOrEmpty(vId))
            {
                youtubeVideoId = vId;
                return string.Empty;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                images.Add((url, alt));
                return string.Empty;
            }

            return match.Value;
        });

        // 3. If no YouTube embed from markdown syntax, check for bare YouTube URLs in the text
        if (string.IsNullOrEmpty(youtubeVideoId))
        {
            var match = YouTubeUrlRegex.Match(cleaned);
            if (match.Success)
            {
                youtubeVideoId = match.Groups[1].Value;
            }
        }

        // 4. Resolve any remaining bare media:// references
        cleaned = BareMediaRegex.Replace(cleaned, match =>
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var mediaId))
            {
                return urlResolver.GetMediaUrl(mediaId);
            }
            return match.Value;
        });

        // 5. Convert markdown text to HTML using Markdig
        var bodyHtml = Markdown.ToHtml(cleaned.Trim(), Pipeline).Trim();

        // 6. Assemble embeds HTML
        var embedsHtml = new StringBuilder();
        foreach (var img in images)
        {
            embedsHtml.AppendLine($@"                <div class=""embed-image-wrapper"">
                    <img src=""{WebUtility.HtmlEncode(img.Url)}"" alt=""{WebUtility.HtmlEncode(img.Alt)}"" loading=""lazy"" />
                </div>");
        }

        if (!string.IsNullOrEmpty(youtubeVideoId))
        {
            embedsHtml.AppendLine($@"                <div class=""embed-video-wrapper"">
                    <iframe src=""https://www.youtube-nocookie.com/embed/{youtubeVideoId}"" title=""YouTube video player"" frameborder=""0"" allow=""accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"" allowfullscreen loading=""lazy""></iframe>
                </div>");
        }

        var charCount = rawSegment.Length;

        return $@"            <div class=""segment"">
                <div class=""seg-badge"">Post {index}/{total} ({charCount} chars)</div>
                <div class=""seg-content"">
                    {bodyHtml}
{embedsHtml}
                </div>
            </div>";
    }

    private static string CleanPlainTextForMeta(string rawSegment)
    {
        if (string.IsNullOrWhiteSpace(rawSegment)) return string.Empty;
        var withoutImages = MediaEmbedRegex.Replace(rawSegment, string.Empty);
        withoutImages = YouTubeEmbedRegex.Replace(withoutImages, string.Empty);
        withoutImages = BareMediaRegex.Replace(withoutImages, string.Empty);
        return Regex.Replace(withoutImages, @"\s+", " ").Trim();
    }
}
