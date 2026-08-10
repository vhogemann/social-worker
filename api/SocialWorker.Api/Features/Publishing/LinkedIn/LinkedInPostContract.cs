using System;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public enum LinkedInMediaKind
{
    None,
    Article,
    Image,
    VideoFuture
}

public enum LinkedInContractError
{
    ThreadNotSupportedForLinkedIn,
    InvalidSegmentLength,
    UnsupportedCombination,
    MissingMediaAsset,
    MediaUploadFailed,
    UnsupportedMvpFeature
}

public class LinkedInContractException : Exception
{
    public LinkedInContractError ErrorType { get; }

    public LinkedInContractException(LinkedInContractError errorType, string message) : base(message)
    {
        ErrorType = errorType;
    }
}

public sealed record LinkedInArticlePayload(string OriginalUrl, string? Title = null, string? Description = null);
public sealed record LinkedInImagePayload(string MediaAssetId, string? AltText = null);
public sealed record LinkedInVideoPayload(string MediaAssetId, string? Title = null, string? Description = null);

public sealed record LinkedInPostContract
{
    public string AuthorUrn { get; }
    public string BodyText { get; }
    public string Visibility { get; }
    public LinkedInMediaKind MediaKind { get; }
    public LinkedInArticlePayload? Article { get; }
    public LinkedInImagePayload? Image { get; }
    public LinkedInVideoPayload? Video { get; }
    
    // Traceability
    public int SourceSegmentIndex { get; }
    public int DraftId { get; }
    public int ThreadId { get; }

    private LinkedInPostContract(
        string authorUrn,
        string bodyText,
        string visibility,
        LinkedInMediaKind mediaKind,
        LinkedInArticlePayload? article,
        LinkedInImagePayload? image,
        LinkedInVideoPayload? video,
        int sourceSegmentIndex,
        int draftId,
        int threadId)
    {
        AuthorUrn = authorUrn;
        BodyText = bodyText;
        Visibility = visibility;
        MediaKind = mediaKind;
        Article = article;
        Image = image;
        Video = video;
        SourceSegmentIndex = sourceSegmentIndex;
        DraftId = draftId;
        ThreadId = threadId;
    }

    public static LinkedInPostContract Create(
        string authorUrn,
        string bodyText,
        string visibility,
        LinkedInMediaKind mediaKind,
        LinkedInArticlePayload? article,
        LinkedInImagePayload? image,
        LinkedInVideoPayload? video,
        int sourceSegmentIndex,
        int draftId,
        int threadId)
    {
        if (string.IsNullOrWhiteSpace(authorUrn))
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, "Author URN is required.");
        }

        if (bodyText == null)
        {
            throw new ArgumentNullException(nameof(bodyText));
        }

        if (bodyText.Length > 3000)
        {
            throw new LinkedInContractException(LinkedInContractError.InvalidSegmentLength, $"Post exceeds 3000 character limit ({bodyText.Length} characters).");
        }

        if (mediaKind == LinkedInMediaKind.VideoFuture)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedMvpFeature, "Video publishing is not supported in the MVP.");
        }

        int mediaCount = 0;
        if (article != null) mediaCount++;
        if (image != null) mediaCount++;
        if (video != null) mediaCount++;

        if (mediaKind == LinkedInMediaKind.None && mediaCount > 0)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, "Media payload provided but MediaKind is None.");
        }

        if (mediaKind != LinkedInMediaKind.None && mediaCount != 1)
        {
            throw new LinkedInContractException(LinkedInContractError.UnsupportedCombination, $"MediaKind is {mediaKind} but found {mediaCount} media payloads.");
        }

        if (mediaKind == LinkedInMediaKind.Article && article == null)
        {
            throw new LinkedInContractException(LinkedInContractError.MissingMediaAsset, "Article payload is required for Article media kind.");
        }

        if (mediaKind == LinkedInMediaKind.Image && image == null)
        {
            throw new LinkedInContractException(LinkedInContractError.MissingMediaAsset, "Image payload is required for Image media kind.");
        }

        if (image != null && string.IsNullOrWhiteSpace(image.MediaAssetId))
        {
            throw new LinkedInContractException(LinkedInContractError.MissingMediaAsset, "Image payload must contain a valid MediaAssetId.");
        }

        return new LinkedInPostContract(authorUrn, bodyText, visibility, mediaKind, article, image, video, sourceSegmentIndex, draftId, threadId);
    }
}
