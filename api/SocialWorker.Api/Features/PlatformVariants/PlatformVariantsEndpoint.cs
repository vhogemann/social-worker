using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.PlatformVariants;

public static class PlatformVariantsEndpoint
{
    public static void MapPlatformVariantsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts").RequireAuthorization();

        group.MapPost("/{canonicalDraftId:guid}/generate-variants", async (
            ClaimsPrincipal principal,
            PlatformVariantService variantService,
            Guid canonicalDraftId,
            GenerateVariantsRequest req,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            if (req.Platforms == null || req.Platforms.Count == 0)
            {
                return Results.BadRequest("At least one platform is required.");
            }

            try
            {
                var result = await variantService.GenerateVariantsAsync(userId.Value, canonicalDraftId, req.Platforms, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapGet("/{draftId:guid}/family", async (
            ClaimsPrincipal principal,
            PlatformVariantService variantService,
            Guid draftId,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var result = await variantService.GetDraftFamilyAsync(userId.Value, draftId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapGet("/{draftId:guid}/variants", async (
            ClaimsPrincipal principal,
            PlatformVariantService variantService,
            Guid draftId,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var result = await variantService.GetVariantsForDraftAsync(userId.Value, draftId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapGet("/canonical/{canonicalDraftId:guid}/variant/{platform}", async (
            ClaimsPrincipal principal,
            PlatformVariantService variantService,
            Guid canonicalDraftId,
            string platform,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var result = await variantService.GetVariantAsync(userId.Value, canonicalDraftId, platform, ct);
            if (result == null) return Results.NotFound();

            return Results.Ok(result);
        });
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}