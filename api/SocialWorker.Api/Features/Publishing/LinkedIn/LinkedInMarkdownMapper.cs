using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public static class LinkedInMarkdownMapper
{
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]]+)\]\((https?://[^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex RawUrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled);

    public static LinkedInPostContract Map(
        string content,
        string authorUrn,
        string visibility,
        Guid draftId,
        Guid threadId,
        Func<Guid, (string? FilePath, string? MimeType, string? AltText)?> getMediaAsset)
    {
        var doc = ThreadMarkdownDocument.Parse(content);
        var nonEmpties = doc.Segments.Where(s => !string.IsNullOrWhiteSpace(s.PlainText) || s.MediaEmbeds.Count > 0 || s.YouTubeEmbed != null).ToList();

        if (nonEmpties.Count == 0)
        {
            throw new LinkedInContractException(LinkedInContractError.InvalidSegmentLength, "Content cannot be empty.");
        }
        if (nonEmpties.Count > 1)
        {
            throw new LinkedInContractException(LinkedInContractError.ThreadNotSupportedForLinkedIn, "LinkedIn publishing does not support multi-post threads. Exactly one segment is allowed.");
        }

        var segDoc = nonEmpties[0];
        var mediaRefs = segDoc.MediaEmbeds;
        if (mediaRefs.Count > 1)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, "Multiple image attachments are not supported.");
        }

        string? youtubeUrl = segDoc.YouTubeEmbed?.Url;
        string? youtubeTitle = segDoc.YouTubeEmbed?.Title;

        string? linkUrl = null;
        string? linkTitle = null;

        var textLink = segDoc.TextLinks.FirstOrDefault();
        if (textLink != null)
        {
            linkTitle = textLink.Text;
            linkUrl = textLink.Url;
        }
        else
        {
            var bareUrl = segDoc.BareUrls.FirstOrDefault(b => !ThreadSegmentDocument.IsYouTubeUrl(b.Url));
            if (bareUrl != null)
            {
                linkUrl = bareUrl.Url;
            }
        }

        bool hasImage = mediaRefs.Count == 1;
        bool hasUrl = !string.IsNullOrEmpty(youtubeUrl) || !string.IsNullOrEmpty(linkUrl);
        if (hasImage && hasUrl)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, "Cannot mix image attachments and URL link shares in a single LinkedIn post.");
        }

        var normalizedBodyText = CleanAndNormalizeBodyText(segDoc.PlainText);


        LinkedInMediaKind mediaKind = LinkedInMediaKind.None;
        LinkedInArticlePayload? article = null;
        LinkedInImagePayload? image = null;

        if (hasImage)
        {
            var mediaRef = mediaRefs[0];
            var asset = getMediaAsset(mediaRef.MediaId);
            if (asset == null)
            {
                throw new LinkedInContractException(LinkedInContractError.MissingMediaAsset, $"Media asset {mediaRef.MediaId} could not be resolved.");
            }

            mediaKind = LinkedInMediaKind.Image;
            var altText = !string.IsNullOrWhiteSpace(asset.Value.AltText) ? asset.Value.AltText : mediaRef.AltText;
            image = new LinkedInImagePayload(mediaRef.MediaId.ToString(), altText);
        }
        else if (!string.IsNullOrEmpty(youtubeUrl))
        {
            mediaKind = LinkedInMediaKind.Article;
            string? thumbUrl = null;
            var videoId = TryExtractYouTubeVideoId(youtubeUrl);
            if (!string.IsNullOrEmpty(videoId))
            {
                thumbUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
            }
            article = new LinkedInArticlePayload(youtubeUrl, youtubeTitle ?? youtubeUrl, ThumbnailUrl: thumbUrl);
        }
        else if (!string.IsNullOrEmpty(linkUrl))
        {
            mediaKind = LinkedInMediaKind.Article;
            string? thumbUrl = null;
            if (ThreadSegmentDocument.IsYouTubeUrl(linkUrl))
            {
                var videoId = TryExtractYouTubeVideoId(linkUrl);
                if (!string.IsNullOrEmpty(videoId))
                {
                    thumbUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
                }
            }
            article = new LinkedInArticlePayload(linkUrl, linkTitle ?? linkUrl, ThumbnailUrl: thumbUrl);
        }

        return LinkedInPostContract.Create(
            authorUrn: authorUrn,
            bodyText: normalizedBodyText,
            visibility: visibility,
            mediaKind: mediaKind,
            article: article,
            image: image,
            video: null,
            sourceSegmentIndex: 0,
            draftId: draftId,
            threadId: threadId
        );
    }

    private static string CleanAndNormalizeBodyText(string content)
    {
        var cleaned = PlatformContentPolicyBase.BoldAsteriskRegex.Replace(content, "$1");
        cleaned = PlatformContentPolicyBase.BoldUnderscoreRegex.Replace(cleaned, "$1");
        cleaned = PlatformContentPolicyBase.ItalicAsteriskRegex.Replace(cleaned, "$1");
        cleaned = PlatformContentPolicyBase.HeadingRegex.Replace(cleaned, "");
        cleaned = MarkdownLinkRegex.Replace(cleaned, "$1");
        return cleaned.Trim();
    }

    public static string? TryExtractYouTubeVideoId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri.AbsolutePath.Trim('/');
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }

        if (!uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("live/", StringComparison.OrdinalIgnoreCase))
        {
            var pieces = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length >= 2 && !string.IsNullOrWhiteSpace(pieces[1]))
            {
                return pieces[1];
            }
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 2 && string.Equals(pieces[0], "v", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pieces[1]);
            }
        }

        return null;
    }
}
