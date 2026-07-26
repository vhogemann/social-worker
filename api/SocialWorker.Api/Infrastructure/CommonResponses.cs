using System.Text.Json.Serialization;

namespace SocialWorker.Api.Infrastructure;

public sealed record ApiSuccessResponse(
    [property: JsonPropertyName("success")] bool Success = true,
    [property: JsonPropertyName("message")] string? Message = null);

public sealed record ApiErrorResponse(
    [property: JsonPropertyName("error")] string Error);

public sealed record DatabaseResetResponse(
    [property: JsonPropertyName("reset")] bool Reset = true);
