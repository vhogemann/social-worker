using System;
using System.Collections.Generic;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public static class LinkedInPayloadMapper
{
    public static LinkedInUgcPostRequest MapToUgcPostRequest(LinkedInPostContract contract, string? uploadedMediaUrn = null)
    {
        var request = new LinkedInUgcPostRequest
        {
            Author = contract.AuthorUrn,
            Visibility = new LinkedInVisibility
            {
                MemberNetworkVisibility = contract.Visibility
            },
            SpecificContent = new LinkedInSpecificContent
            {
                ShareContent = new LinkedInShareContent
                {
                    ShareCommentary = new LinkedInShareCommentary
                    {
                        Text = contract.BodyText
                    }
                }
            }
        };

        var shareContent = request.SpecificContent.ShareContent;

        switch (contract.MediaKind)
        {
            case LinkedInMediaKind.None:
                shareContent.ShareMediaCategory = "NONE";
                break;

            case LinkedInMediaKind.Article:
                if (contract.Article == null)
                {
                    throw new InvalidOperationException("Article payload missing for Article media kind.");
                }

                shareContent.ShareMediaCategory = "ARTICLE";
                var shareMedia = new LinkedInShareMedia
                {
                    Status = "READY",
                    OriginalUrl = contract.Article.OriginalUrl,
                    Title = !string.IsNullOrEmpty(contract.Article.Title) 
                        ? new LinkedInTextObject { Text = contract.Article.Title } 
                        : null,
                    Description = !string.IsNullOrEmpty(contract.Article.Description) 
                        ? new LinkedInTextObject { Text = contract.Article.Description } 
                        : null
                };

                if (!string.IsNullOrWhiteSpace(contract.Article.ThumbnailUrl))
                {
                    shareMedia.Thumbnails = new List<LinkedInThumbnail>
                    {
                        new() { Url = contract.Article.ThumbnailUrl }
                    };
                }

                shareContent.Media = new List<LinkedInShareMedia> { shareMedia };
                break;

            case LinkedInMediaKind.Image:
                if (contract.Image == null)
                {
                    throw new InvalidOperationException("Image payload missing for Image media kind.");
                }

                if (string.IsNullOrEmpty(uploadedMediaUrn))
                {
                    throw new InvalidOperationException("Uploaded media URN is required for Image media kind.");
                }

                shareContent.ShareMediaCategory = "IMAGE";
                shareContent.Media = new List<LinkedInShareMedia>
                {
                    new()
                    {
                        Status = "READY",
                        MediaUrn = uploadedMediaUrn,
                        Description = !string.IsNullOrEmpty(contract.Image.AltText)
                            ? new LinkedInTextObject { Text = contract.Image.AltText }
                            : null
                    }
                };
                break;

            default:
                throw new NotSupportedException($"Media kind {contract.MediaKind} is not supported.");
        }

        return request;
    }
}
