using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Sources.Instagram;

public sealed record InstagramBusinessDiscoveryRootResponse(
    [property: JsonPropertyName("business_discovery")] InstagramProfileDiscovery? BusinessDiscovery,
    [property: JsonPropertyName("id")] string? CallerId,
    [property: JsonPropertyName("error")] InstagramApiError? Error
);

public sealed record InstagramProfileDiscovery(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("profile_picture_url")] string? ProfilePictureUrl,
    [property: JsonPropertyName("biography")] string? Biography,
    [property: JsonPropertyName("media")] InstagramMediaList? Media
);

public sealed record InstagramMediaList(
    [property: JsonPropertyName("data")] List<InstagramMediaItem>? Data,
    [property: JsonPropertyName("paging")] InstagramPaging? Paging
);

public sealed class InstagramMediaItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("caption")] public string? Caption { get; set; }
    [JsonPropertyName("media_type")] public string? MediaType { get; set; }
    [JsonPropertyName("media_url")] public string? MediaUrl { get; set; }
    [JsonPropertyName("thumbnail_url")] public string? ThumbnailUrl { get; set; }
    [JsonPropertyName("permalink")] public string? Permalink { get; set; }
    [JsonPropertyName("timestamp")] public string? TimestampRaw { get; set; }
    [JsonPropertyName("like_count")] public int? LikeCount { get; set; }
    [JsonPropertyName("comments_count")] public int? CommentsCount { get; set; }

    [JsonIgnore]
    public DateTime? Timestamp => DateTimeOffset.TryParse(TimestampRaw, out var dto) ? dto.UtcDateTime : null;

    public InstagramMediaItem() { }

    public InstagramMediaItem(
        string Id,
        string? Caption,
        string? MediaType,
        string? MediaUrl,
        string? ThumbnailUrl,
        string? Permalink,
        DateTime? Timestamp,
        int? LikeCount = null,
        int? CommentsCount = null)
    {
        this.Id = Id;
        this.Caption = Caption;
        this.MediaType = MediaType;
        this.MediaUrl = MediaUrl;
        this.ThumbnailUrl = ThumbnailUrl;
        this.Permalink = Permalink;
        this.TimestampRaw = Timestamp?.ToString("o");
        this.LikeCount = LikeCount;
        this.CommentsCount = CommentsCount;
    }
}

public sealed record InstagramPaging(
    [property: JsonPropertyName("cursors")] InstagramCursors? Cursors
);

public sealed record InstagramCursors(
    [property: JsonPropertyName("after")] string? After,
    [property: JsonPropertyName("before")] string? Before
);

public sealed record InstagramApiError(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("error_subcode")] int? ErrorSubcode,
    [property: JsonPropertyName("fbtrace_id")] string? FbTraceId
);

public sealed record MetaAccountsResponse(
    [property: JsonPropertyName("data")] List<MetaPageItem>? Data,
    [property: JsonPropertyName("error")] InstagramApiError? Error
);

public sealed record MetaPageItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("instagram_business_account")] MetaInstagramBusinessAccount? InstagramBusinessAccount
);

public sealed record MetaInstagramBusinessAccount(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("profile_picture_url")] string? ProfilePictureUrl
);

public sealed record MetaMeInstagramResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("instagram_business_account")] MetaInstagramBusinessAccount? InstagramBusinessAccount,
    [property: JsonPropertyName("error")] InstagramApiError? Error
);

public sealed record InstagramTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn
);

public sealed record InstagramCallbackRequest(string Code, string State);

public sealed record InstagramCallbackResponse(string ExternalId, string Username, DateTime? TokenExpiresAt);
