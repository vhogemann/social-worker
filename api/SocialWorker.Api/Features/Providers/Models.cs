using System;

namespace SocialWorker.Api.Features.Providers;

public static class ProviderModels
{
    public sealed record LlmProviderDto(
        Guid Id,
        string Name,
        string ProviderType,
        string BaseUrl,
        bool ApiKeySet,
        string Model,
        bool IsDefault,
        bool IsActive,
        bool SupportsVision,
        bool SupportsTools
    );

    public sealed record CreateProviderRequest(
        string Name,
        string ProviderType,
        string BaseUrl,
        string ApiKey,
        string Model
    );

    public sealed record UpdateProviderRequest(
        string? Name,
        string? ProviderType,
        string? BaseUrl,
        string? ApiKey,
        string? Model,
        bool? IsDefault,
        bool? IsActive
    );

    public sealed record AvailableProviderDto(
        Guid Id,
        string Name,
        string ProviderType,
        string Model
    );

    public sealed record TestProviderRequest(
        string ProviderType,
        string BaseUrl,
        string ApiKey,
        string Model
    );

    public sealed record TestProviderResponse(
        bool Success,
        string? Error
    );
}
