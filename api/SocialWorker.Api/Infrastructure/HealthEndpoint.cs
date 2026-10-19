using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SocialWorker.Api.Infrastructure;

public sealed record HealthStatusResponse(string Status, DateTimeOffset Timestamp);

public static class HealthEndpoint
{
    public static HealthStatusResponse GetHealth() => new("healthy", DateTimeOffset.UtcNow);

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(GetHealth())).AllowAnonymous();
    }
}
