using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed record BlueskyCreateSessionRequest(string Identifier, string Password);

public sealed class BlueskyCreateSessionResponse
{
    [JsonPropertyName("accessJwt")]
    public string? AccessJwt { get; init; }

    [JsonPropertyName("did")]
    public string? Did { get; init; }
}

public sealed record BlueskyCreateRecordRequest(string Repo, string Collection, BlueskyPostRecord Record);

public sealed class BlueskyLikeRecord
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = "app.bsky.feed.like";

    [JsonPropertyName("subject")]
    public BlueskyRecordRef Subject { get; init; } = null!;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; init; } = null!;
}

public sealed record BlueskyCreateLikeRecordRequest(string Repo, string Collection, BlueskyLikeRecord Record);

public sealed class BlueskyCreateRecordResponse
{
    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    [JsonPropertyName("cid")]
    public string? Cid { get; init; }
}

public sealed class BlueskyUploadBlobResponse
{
    [JsonPropertyName("blob")]
    public JsonElement Blob { get; init; }
}

public sealed class YouTubeOEmbedResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("thumbnail_url")]
    public string? ThumbnailUrl { get; init; }
}
