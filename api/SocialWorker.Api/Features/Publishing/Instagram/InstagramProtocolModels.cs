using System;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.Instagram;

public class InstagramApiException : Exception
{
    public int? StatusCode { get; }
    public string? ErrorType { get; }
    public int? ErrorCode { get; }

    public InstagramApiException(string message, int? statusCode = null, string? errorType = null, int? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        ErrorCode = errorCode;
    }
}

public record InstagramContainerResponse(
    [property: JsonPropertyName("id")] string Id);

public record InstagramContainerStatusResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status_code")] string StatusCode,
    [property: JsonPropertyName("status")] string? Status = null);

public record InstagramPublishResponse(
    [property: JsonPropertyName("id")] string Id);

public record InstagramMediaPermalinkResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("permalink")] string? Permalink = null,
    [property: JsonPropertyName("shortcode")] string? Shortcode = null);
