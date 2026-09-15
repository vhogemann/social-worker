using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure;

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
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var hasAccountIds = req.AccountIds != null && req.AccountIds.Count > 0;
            var hasPlatforms = req.Platforms != null && req.Platforms.Count > 0;

            if (!hasAccountIds && !hasPlatforms)
            {
                return Results.BadRequest(new ApiErrorResponse("At least one platform or account is required."));
            }

            try
            {
                var result = await variantService.GenerateVariantsAsync(userId.Value, canonicalDraftId, req.Platforms, req.AccountIds, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ApiErrorResponse("Draft not found."));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiErrorResponse(ex.Message));
            }
        });

        group.MapGet("/{draftId:guid}/family", async (
            ClaimsPrincipal principal,
            PlatformVariantService variantService,
            Guid draftId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
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
            var userId = principal.GetUserId();
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
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await variantService.GetVariantAsync(userId.Value, canonicalDraftId, platform, ct);
            if (result == null) return Results.NotFound();

            return Results.Ok(result);
        });
    }

    }