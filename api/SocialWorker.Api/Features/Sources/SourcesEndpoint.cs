using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Sources;

public static class SourcesEndpoint
{
    public static void MapSourcesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts/{draftId:guid}").RequireAuthorization();

        group.MapGet("/sources", async (ClaimsPrincipal principal, SourcesService sourcesService, Guid draftId, CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var sources = await sourcesService.GetSourcesForDraftAsync(userId.Value, draftId, ct);
                return Results.Ok(sources);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapGet("/sources/{sourceId:guid}", async (ClaimsPrincipal principal, SourcesService sourcesService, Guid draftId, Guid sourceId, CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var source = await sourcesService.GetSourceDetailAsync(userId.Value, draftId, sourceId, ct);
                return Results.Ok(source);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapDelete("/sources/{sourceId:guid}", async (ClaimsPrincipal principal, SourcesService sourcesService, Guid draftId, Guid sourceId, CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                await sourcesService.DeleteSourceAsync(userId.Value, draftId, sourceId, ct);
                return Results.Ok(new { success = true });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/files", async (
            ClaimsPrincipal principal,
            SourcesService sourcesService,
            SourceExtractor extractor,
            Guid draftId,
            IFormFile file,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await sourcesService.AddFileSourceAsync(userId.Value, draftId, file.FileName, stream, extractor, ct);
                return Results.Ok(new
                {
                    sourceId = result.SourceId,
                    markdownLink = $"[File: {result.Reference}](file://{result.SourceId})"
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (NotSupportedException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to process file attachment: {ex.Message}");
            }
        }).DisableAntiforgery();

        group.MapPost("/sources/import-url", async (
            ClaimsPrincipal principal,
            SourcesService sourcesService,
            Guid draftId,
            ImportSourceFromUrlRequest req,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            if (req is null || string.IsNullOrWhiteSpace(req.Url))
            {
                return Results.BadRequest("A valid source URL is required.");
            }

            try
            {
                var result = await sourcesService.AddUrlSourceAsync(
                    userId.Value,
                    draftId,
                    req.Url,
                    req.Title,
                    req.Content,
                    ct);

                return Results.Ok(new
                {
                    sourceId = result.SourceId,
                    result.Reference,
                    result.Title,
                    result.Kind
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}

public sealed record ImportSourceFromUrlRequest(string Url, string? Title, string? Content);
