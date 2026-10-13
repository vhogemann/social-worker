using System;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.Twitter;

public sealed record TwitterAccountCredentials(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
    [property: JsonPropertyName("token_type")] string TokenType = "bearer",
    [property: JsonPropertyName("scope")] string? Scope = null);

public sealed record TwitterTokenResponse(
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);

public sealed record TwitterUserData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("username")] string Username);

public sealed record TwitterUserResponse(
    [property: JsonPropertyName("data")] TwitterUserData? Data);

public sealed record TwitterReplyOptions(
    [property: JsonPropertyName("in_reply_to_tweet_id")] string InReplyToTweetId);

public sealed record TwitterMediaOptions(
    [property: JsonPropertyName("media_ids")] IReadOnlyList<string> MediaIds);

public sealed record TwitterCreateTweetRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("reply"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TwitterReplyOptions? Reply = null,
    [property: JsonPropertyName("media"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TwitterMediaOptions? Media = null);

public sealed record TwitterMediaInitRequest(
    [property: JsonPropertyName("total_bytes")] long TotalBytes,
    [property: JsonPropertyName("media_type")] string MediaType,
    [property: JsonPropertyName("media_category")] string MediaCategory);

public sealed record TwitterMediaData(
    [property: JsonPropertyName("id")] string Id);

public sealed record TwitterMediaInitResponse(
    [property: JsonPropertyName("data")] TwitterMediaData? Data);

public sealed record TwitterTweetData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text);

public sealed record TwitterCreateTweetResponse(
    [property: JsonPropertyName("data")] TwitterTweetData? Data);

public sealed record TwitterErrorResponse(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("detail")] string? Detail,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("status")] int? Status);

public sealed record TwitterSearchTweetItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("author_id")] string? AuthorId,
    [property: JsonPropertyName("created_at")] DateTime? CreatedAt,
    [property: JsonPropertyName("in_reply_to_user_id")] string? InReplyToUserId,
    [property: JsonPropertyName("conversation_id")] string? ConversationId);

public sealed record TwitterSearchIncludes(
    [property: JsonPropertyName("users")] IReadOnlyList<TwitterUserData>? Users);

public sealed record TwitterSearchTweetsResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<TwitterSearchTweetItem>? Data,
    [property: JsonPropertyName("includes")] TwitterSearchIncludes? Includes);

