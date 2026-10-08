using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.MetaThreads;

public sealed record MetaThreadsTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("user_id")] string? UserId
);

public sealed record MetaThreadsLongLivedTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] long ExpiresIn
);

public sealed record MetaThreadsUserProfile(
    [property: JsonPropertyName("id")] string Id,
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
    [property: JsonPropertyName("id")] string Id
);

public sealed record MetaThreadsContainerStatusResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("error_message")] string? ErrorMessage
);

public sealed record MetaThreadsPublishResponse(
    [property: JsonPropertyName("id")] string Id
);
