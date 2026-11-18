using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.Mastodon;

public sealed record MastodonCredentials(
    [property: JsonPropertyName("instanceUrl")] string InstanceUrl,
    [property: JsonPropertyName("accessToken")] string AccessToken);

public sealed record MastodonOAuthStatePayload(
    [property: JsonPropertyName("instanceUrl")] string InstanceUrl,
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientSecret")] string ClientSecret,
    [property: JsonPropertyName("redirectUri")] string RedirectUri);

public sealed record MastodonAppRegistrationRequest(
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] string RedirectUris,
    [property: JsonPropertyName("scopes")] string Scopes,
    [property: JsonPropertyName("website")] string? Website = null);

public sealed record MastodonAppRegistrationResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string ClientSecret);

public sealed record MastodonTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("created_at")] long? CreatedAt);

public sealed record MastodonAccountProfile(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("acct")] string Acct,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("url")] string? Url);

public sealed record MastodonMediaUploadResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("preview_url")] string? PreviewUrl);

public sealed record MastodonStatusRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("in_reply_to_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InReplyToId = null,
    [property: JsonPropertyName("media_ids"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? MediaIds = null,
    [property: JsonPropertyName("visibility")] string Visibility = "public",
    [property: JsonPropertyName("spoiler_text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SpoilerText = null);

public sealed record MastodonStatusResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("uri")] string? Uri,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("in_reply_to_id")] string? InReplyToId,
    [property: JsonPropertyName("visibility")] string? Visibility,
    [property: JsonPropertyName("account")] MastodonAccountProfile? Account,
    [property: JsonPropertyName("favourited")] bool? Favourited,
    [property: JsonPropertyName("replies_count")] int? RepliesCount);

public sealed record MastodonStatusContextResponse(
    [property: JsonPropertyName("ancestors")] IReadOnlyList<MastodonStatusResponse> Ancestors,
    [property: JsonPropertyName("descendants")] IReadOnlyList<MastodonStatusResponse> Descendants);
