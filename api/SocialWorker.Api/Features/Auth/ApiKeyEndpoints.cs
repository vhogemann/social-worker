using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Auth;

namespace SocialWorker.Api.Features.Auth;

public static class ApiKeyEndpoints
{
    public static void MapApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/api-keys").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApiKeyService apiKeyService, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var keys = await apiKeyService.ListApiKeysAsync(userId.Value, ct);
            return Results.Ok(keys);
        });

        group.MapPost("/", async (ClaimsPrincipal principal, ApiKeyService apiKeyService, CreateApiKeyRequest req, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Name is required.");
            }

            var result = await apiKeyService.CreateApiKeyAsync(userId.Value, req.Name, req.Scopes, req.ExpiresAt, ct);
            return Results.Created($"/api/api-keys/{result.Id}", result);
        });

        group.MapDelete("/{id:guid}", async (ClaimsPrincipal principal, ApiKeyService apiKeyService, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var revoked = await apiKeyService.RevokeApiKeyAsync(userId.Value, id, ct);
            return revoked ? Results.NoContent() : Results.NotFound();
        });
    }
}

public sealed record CreateApiKeyRequest(string Name, string? Scopes = null, DateTime? ExpiresAt = null);
