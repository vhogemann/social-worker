using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure;

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
        var segments = DraftSegmentService.SplitMarkdownIntoSegments(content);
        var nonEmpties = segments.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        if (nonEmpties.Count == 0)
        {
            throw new LinkedInContractException(LinkedInContractError.InvalidSegmentLength, "Content cannot be empty.");
        }
        if (nonEmpties.Count > 1)
        {
            throw new LinkedInContractException(LinkedInContractError.ThreadNotSupportedForLinkedIn, "LinkedIn publishing does not support multi-post threads. Exactly one segment is allowed.");
        }

        var rawSegment = nonEmpties[0];
        var mediaRefs = SharedPatterns.ExtractMediaReferences(rawSegment);
        if (mediaRefs.Count > 1)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, "Multiple image attachments are not supported.");
        }

        var youtubeMatch = SharedPatterns.YoutubeMarkdownRegex.Match(rawSegment);
        string? youtubeUrl = null;
        string? youtubeTitle = null;

        if (youtubeMatch.Success)
        {
            youtubeTitle = youtubeMatch.Groups[1].Value;
            youtubeUrl = youtubeMatch.Groups[2].Value;
        }
        else
        {
            var rawMatch = RawUrlRegex.Matches(rawSegment)
                .Cast<Match>()
                .FirstOrDefault(m => m.Value.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                                     m.Value.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase));
            if (rawMatch != null)
            {
                youtubeUrl = rawMatch.Value;
            }
        }

        string? linkUrl = null;
        string? linkTitle = null;

        var linkMatch = MarkdownLinkRegex.Matches(rawSegment)
            .Cast<Match>()
            .FirstOrDefault(m => !m.Value.StartsWith("!"));
        if (linkMatch != null)
        {
            linkTitle = linkMatch.Groups[1].Value;
            linkUrl = linkMatch.Groups[2].Value;
        }
        else
        {
            var rawMatch = RawUrlRegex.Matches(rawSegment)
                .Cast<Match>()
                .FirstOrDefault(m => !m.Value.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) &&
                                     !m.Value.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase));
            if (rawMatch != null)
            {
                linkUrl = rawMatch.Value;
            }
        }

        bool hasImage = mediaRefs.Count == 1;
        bool hasUrl = !string.IsNullOrEmpty(youtubeUrl) || !string.IsNullOrEmpty(linkUrl);
        if (hasImage && hasUrl)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, "Cannot mix image attachments and URL link shares in a single LinkedIn post.");
        }

        var bodyWithNoMedia = SharedPatterns.StripMediaMarkdown(rawSegment);
        if (youtubeMatch.Success)
        {
            bodyWithNoMedia = SharedPatterns.YoutubeMarkdownRegex.Replace(bodyWithNoMedia, "");
        }
        var normalizedBodyText = CleanAndNormalizeBodyText(bodyWithNoMedia);

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
            article = new LinkedInArticlePayload(youtubeUrl, youtubeTitle ?? youtubeUrl);
        }
        else if (!string.IsNullOrEmpty(linkUrl))
        {
            mediaKind = LinkedInMediaKind.Article;
            article = new LinkedInArticlePayload(linkUrl, linkTitle ?? linkUrl);
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
}
