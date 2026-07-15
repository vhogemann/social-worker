namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed record BlueskyApiCallResult<T>(bool Success, T? Value = default, string? Error = null);
