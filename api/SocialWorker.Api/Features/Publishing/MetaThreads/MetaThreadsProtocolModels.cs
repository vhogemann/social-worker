using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.MetaThreads;

public sealed class MetaThreadsStringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDecimal().ToString(),
            JsonTokenType.Null => null,
            _ => reader.GetString()
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

public sealed record MetaThreadsTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("user_id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string? UserId
);

public sealed record MetaThreadsLongLivedTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] long ExpiresIn
);

public sealed record MetaThreadsUserProfile(
    [property: JsonPropertyName("id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("threads_profile_picture_url")] string? ProfilePictureUrl
);

public sealed record MetaThreadsApiErrorDetails(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("error_subcode")] int? ErrorSubcode
);

public sealed record MetaThreadsApiErrorResponse(
    [property: JsonPropertyName("error")] MetaThreadsApiErrorDetails? Error
);

public sealed record MetaThreadsContainerResponse(
    [property: JsonPropertyName("id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string Id
);

public sealed record MetaThreadsContainerStatusResponse(
    [property: JsonPropertyName("id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("error_message")] string? ErrorMessage
);

public sealed record MetaThreadsPublishResponse(
    [property: JsonPropertyName("id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string Id
);

public sealed record MetaThreadsRepliedTo(
    [property: JsonPropertyName("id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string Id
);

public sealed record MetaThreadsReplyItem(
    [property: JsonPropertyName("id"), JsonConverter(typeof(MetaThreadsStringOrNumberConverter))] string Id,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("media_type")] string? MediaType,
    [property: JsonPropertyName("media_url")] string? MediaUrl,
    [property: JsonPropertyName("permalink")] string? Permalink,
    [property: JsonPropertyName("has_replies")] bool? HasReplies,
    [property: JsonPropertyName("replied_to")] MetaThreadsRepliedTo? RepliedTo,
    [property: JsonPropertyName("root_post")] MetaThreadsRepliedTo? RootPost
);

public sealed record MetaThreadsRepliesResponse(
    [property: JsonPropertyName("data")] List<MetaThreadsReplyItem>? Data = null
);

