using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public sealed class LinkedInUgcPostRequest
{
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("lifecycleState")]
    public string LifecycleState { get; set; } = "PUBLISHED";

    [JsonPropertyName("specificContent")]
    public LinkedInSpecificContent SpecificContent { get; set; } = new();

    [JsonPropertyName("visibility")]
    public LinkedInVisibility Visibility { get; set; } = new();
}

public sealed class LinkedInSpecificContent
{
    [JsonPropertyName("com.linkedin.ugc.ShareContent")]
    public LinkedInShareContent ShareContent { get; set; } = new();
}

public sealed class LinkedInShareContent
{
    [JsonPropertyName("shareCommentary")]
    public LinkedInShareCommentary ShareCommentary { get; set; } = new();

    [JsonPropertyName("shareMediaCategory")]
    public string ShareMediaCategory { get; set; } = "NONE";

    [JsonPropertyName("media")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedInShareMedia>? Media { get; set; }
}

public sealed class LinkedInShareCommentary
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class LinkedInShareMedia
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "READY";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedInTextObject? Description { get; set; }

    [JsonPropertyName("originalUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalUrl { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedInTextObject? Title { get; set; }

    [JsonPropertyName("thumbnails")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedInThumbnail>? Thumbnails { get; set; }

    [JsonPropertyName("media")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaUrn { get; set; }
}

public sealed class LinkedInThumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class LinkedInTextObject
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class LinkedInVisibility
{
    [JsonPropertyName("com.linkedin.ugc.MemberNetworkVisibility")]
    public string MemberNetworkVisibility { get; set; } = "PUBLIC";
}

public sealed class LinkedInUgcPostResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public sealed class LinkedInRegisterUploadRequest
{
    [JsonPropertyName("registerRequest")]
    public LinkedInRegisterRequest RegisterRequest { get; set; } = new();
}

public sealed class LinkedInRegisterRequest
{
    [JsonPropertyName("recipes")]
    public List<string> Recipes { get; set; } = new() { "urn:li:digitalmediaRecipe:feedshare-image" };

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("supportedUploadMechanism")]
    public List<string> SupportedUploadMechanism { get; set; } = new() { "SYNCHRONOUS_UPLOAD" };
}

public sealed class LinkedInRegisterUploadResponse
{
    [JsonPropertyName("value")]
    public LinkedInRegisterUploadValue? Value { get; set; }
}

public sealed class LinkedInRegisterUploadValue
{
    [JsonPropertyName("asset")]
    public string? Asset { get; set; }

    [JsonPropertyName("uploadMechanism")]
    public LinkedInUploadMechanism? UploadMechanism { get; set; }
}

public sealed class LinkedInUploadMechanism
{
    [JsonPropertyName("com.linkedin.digitalmedia.uploading.MediaUploadMechanism")]
    public LinkedInMediaUploadMechanism? MediaUploadMechanism { get; set; }
}

public sealed class LinkedInMediaUploadMechanism
{
    [JsonPropertyName("uploadUrl")]
    public string? UploadUrl { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}
