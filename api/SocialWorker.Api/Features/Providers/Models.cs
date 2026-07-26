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
        int? ContextWindowTokens,
        bool IsDefault,
        bool IsActive,
        bool SupportsVision,
        bool SupportsTools
    )
    {
        public LlmProviderDto(Data.Entities.LlmProvider p, Infrastructure.Llm.ModelCapabilities caps)
            : this(
                p.Id,
                p.Name,
                p.ProviderType,
                p.BaseUrl,
                !string.IsNullOrEmpty(p.ApiKey),
                p.Model,
                p.ContextWindowTokens ?? caps.ContextWindowTokens,
                p.IsDefault,
                p.IsActive,
                caps.SupportsVision,
                caps.SupportsTools)
        {
        }
    };

    public sealed record CreateProviderRequest(
        string Name,
        string ProviderType,
        string BaseUrl,
        string ApiKey,
        string Model,
        int? ContextWindowTokens
    );

    public sealed record UpdateProviderRequest(
        string? Name,
        string? ProviderType,
        string? BaseUrl,
        string? ApiKey,
        string? Model,
        int? ContextWindowTokens,
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
        string Model,
        int? ContextWindowTokens
    );

    public sealed record TestProviderResponse(
        bool Success,
        string? Error,
        int? ContextWindowTokens
    );

    public sealed record ProviderOperationResult<TData>(
        TData? Data,
        string? Error,
        bool IsNotFound)
    {
        public bool IsSuccess => Error == null && !IsNotFound;

        public static ProviderOperationResult<TData> Ok(TData data) => new(data, null, false);
        public static ProviderOperationResult<TData> Fail(string error) => new(default, error, false);
        public static ProviderOperationResult<TData> NotFound() => new(default, null, true);
    }
}
