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

public sealed record TwitterCreateTweetRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("reply"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TwitterReplyOptions? Reply = null);

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
