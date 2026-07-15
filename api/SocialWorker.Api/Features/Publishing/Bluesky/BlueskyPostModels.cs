using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed class BlueskyPostRecord
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = "app.bsky.feed.post";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("facets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BlueskyFacet>? Facets { get; init; }

    [JsonPropertyName("embed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlueskyEmbed? Embed { get; init; }

    [JsonPropertyName("reply")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlueskyReply? Reply { get; init; }
}

public sealed class BlueskyReply
{
    [JsonPropertyName("root")]
    public BlueskyRecordRef Root { get; init; } = new(string.Empty, string.Empty);

    [JsonPropertyName("parent")]
    public BlueskyRecordRef Parent { get; init; } = new(string.Empty, string.Empty);
}

public sealed class BlueskyFacet
{
    [JsonPropertyName("index")]
    public BlueskyFacetIndex Index { get; init; } = new();

    [JsonPropertyName("features")]
    public List<BlueskyFacetFeature> Features { get; init; } = new();
}

public sealed class BlueskyFacetIndex
{
    [JsonPropertyName("byteStart")]
    public int ByteStart { get; init; }

    [JsonPropertyName("byteEnd")]
    public int ByteEnd { get; init; }
}

public sealed class BlueskyFacetFeature
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uri { get; init; }

    [JsonPropertyName("tag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tag { get; init; }
}

public sealed class BlueskyEmbed
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BlueskyEmbedImage>? Images { get; init; }

    [JsonPropertyName("external")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlueskyExternal? External { get; init; }
}

public sealed class BlueskyEmbedImage
{
    [JsonPropertyName("alt")]
    public string Alt { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public JsonElement Image { get; init; }
}

public sealed class BlueskyExternal
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("thumb")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Thumb { get; set; }
}
