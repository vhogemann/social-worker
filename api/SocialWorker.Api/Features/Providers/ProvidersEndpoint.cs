using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Features.Providers.Services;

namespace SocialWorker.Api.Features.Providers;

public static class ProvidersEndpoint
{
    private static readonly PlatformCapabilityDto[] PlatformCapabilities =
    {
        new("Bluesky", true),
        new("Twitter", false),
        new("LinkedIn", false),
        new("Facebook", false),
        new("Instagram", false)
    };

    public static void MapProvidersEndpoints(this WebApplication app)
    {
        var adminGroup = app.MapGroup("/api/providers")
            .RequireAuthorization("Admin");

        adminGroup.MapGet("/", async (ProvidersService service, CancellationToken ct) =>
        {
            var providers = await service.GetProvidersAsync(ct);
            return Results.Ok(providers);
        });

        adminGroup.MapPost("/", async (ProvidersService service, ProviderModels.CreateProviderRequest req, CancellationToken ct) =>
        {
            var result = await service.CreateProviderAsync(req, ct);
            if (result.Error != null)
            {
                if (result.Error == "A provider with this name already exists.") return Results.Conflict(result.Error);
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Data);
        });

        adminGroup.MapPatch("/{id:guid}", async (ProvidersService service, Guid id, ProviderModels.UpdateProviderRequest req, CancellationToken ct) =>
        {
            var result = await service.UpdateProviderAsync(id, req, ct);
            
            if (result.IsNotFound) return Results.NotFound();
            
            if (result.Error != null)
            {
                if (result.Error == "A provider with this name already exists.") return Results.Conflict(result.Error);
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Data);
        });

        adminGroup.MapDelete("/{id:guid}", async (ProvidersService service, Guid id, CancellationToken ct) =>
        {
            var result = await service.DeleteProviderAsync(id, ct);
            
            if (result.IsNotFound) return Results.NotFound();
            if (result.Error != null) return Results.BadRequest(result.Error);

            return Results.NoContent();
        });

        adminGroup.MapPost("/test", async (ProvidersService service, ProviderModels.TestProviderRequest req, CancellationToken ct) =>
        {
            var result = await service.TestProviderConnectionAsync(req, ct);
            return Results.Ok(result);
        });

        app.MapGet("/api/providers/available", async (ProvidersService service, CancellationToken ct) =>
        {
            var providers = await service.GetAvailableProvidersAsync(ct);
            return Results.Ok(providers);
        }).RequireAuthorization();

        app.MapGet("/api/providers/platform-capabilities", () => Results.Ok(PlatformCapabilities))
            .RequireAuthorization();
    }
}
