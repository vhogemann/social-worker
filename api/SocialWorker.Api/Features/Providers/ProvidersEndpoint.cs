using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Features.Providers.Services;

namespace SocialWorker.Api.Features.Providers;

public static class ProvidersEndpoint
{
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
            var (dto, error) = await service.CreateProviderAsync(req, ct);
            if (error != null)
            {
                if (error == "A provider with this name already exists.") return Results.Conflict(error);
                return Results.BadRequest(error);
            }

            return Results.Ok(dto);
        });

        adminGroup.MapPatch("/{id:guid}", async (ProvidersService service, Guid id, ProviderModels.UpdateProviderRequest req, CancellationToken ct) =>
        {
            var (dto, error, isNotFound) = await service.UpdateProviderAsync(id, req, ct);
            
            if (isNotFound) return Results.NotFound();
            
            if (error != null)
            {
                if (error == "A provider with this name already exists.") return Results.Conflict(error);
                return Results.BadRequest(error);
            }

            return Results.Ok(dto);
        });

        adminGroup.MapDelete("/{id:guid}", async (ProvidersService service, Guid id, CancellationToken ct) =>
        {
            var (success, error, isNotFound) = await service.DeleteProviderAsync(id, ct);
            
            if (isNotFound) return Results.NotFound();
            if (error != null) return Results.BadRequest(error);

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
    }
}
