using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SocialWorker.Api.Infrastructure.Validation;

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) where TRequest : class
    {
        return builder.AddEndpointFilter<FluentValidationFilter<TRequest>>();
    }

    public static RouteGroupBuilder WithValidation<TRequest>(this RouteGroupBuilder builder) where TRequest : class
    {
        return builder.AddEndpointFilter<FluentValidationFilter<TRequest>>();
    }
}
