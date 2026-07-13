using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SocialWorker.Api.Features.Media;

public static class MediaEndpoint
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/drafts/{draftId:guid}/media", async (
            ClaimsPrincipal principal,
            MediaService mediaService,
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

            var mimeType = file.ContentType.ToLowerInvariant();
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif", "image/bmp", "image/x-bmp" };
            if (!allowedMimeTypes.Contains(mimeType))
            {
                return Results.BadRequest($"Unsupported image format '{mimeType}'. Supported formats: JPG, PNG, WEBP, GIF");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await mediaService.UploadMediaAsync(userId.Value, draftId, file.FileName, mimeType, stream, ct);
                return Results.Ok(new
                {
                    id = result.Id,
                    markdownTag = result.MarkdownTag
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to upload image: {ex.Message}");
            }
        }).RequireAuthorization().DisableAntiforgery();

        app.MapPost("/api/drafts/{draftId:guid}/media/import-url", async (
            ClaimsPrincipal principal,
            IServiceScopeFactory scopeFactory,
            MediaService mediaService,
            Guid draftId,
            ImportMediaFromUrlRequest req,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            if (req is null || string.IsNullOrWhiteSpace(req.Url))
            {
                return Results.BadRequest("A valid image URL is required.");
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                using var client = factory.CreateClient();

                var result = await mediaService.ImportMediaFromUrlAsync(
                    userId.Value,
                    draftId,
                    req.Url,
                    client,
                    ct,
                    req.AltText);

                return Results.Ok(new
                {
                    id = result.Id,
                    markdownTag = result.MarkdownTag
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
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to import image: {ex.Message}");
            }
        }).RequireAuthorization();

        app.MapGet("/api/media/{id:guid}", async (MediaService mediaService, Guid id, CancellationToken ct) =>
        {
            try
            {
                var (fullPath, mimeType) = await mediaService.GetMediaFileAsync(id, ct);
                return Results.File(fullPath, mimeType);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });

        app.MapPatch("/api/media/{id:guid}", async (
            ClaimsPrincipal principal,
            MediaService mediaService,
            Guid id,
            UpdateMediaRequest req,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var asset = await mediaService.UpdateMediaAltTextAsync(userId.Value, id, req.AltText, ct);
                return Results.Ok(new
                {
                    asset.Id,
                    asset.DraftId,
                    asset.FileName,
                    asset.MimeType,
                    asset.AltText,
                    asset.FilePath,
                    asset.SizeBytes,
                    asset.Width,
                    asset.Height
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        app.MapDelete("/api/media/{id:guid}", async (
            ClaimsPrincipal principal,
            MediaService mediaService,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            try
            {
                await mediaService.DeleteMediaAsync(userId.Value, id, ct);
                return Results.Ok(new { success = true });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}

public sealed record UpdateMediaRequest(string? AltText);
public sealed record ImportMediaFromUrlRequest(string Url, string? AltText);
