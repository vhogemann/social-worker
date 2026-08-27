using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Data.Seed;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Templates.Services;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Templates;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/templates");

        group.MapGet("/", async (string? search, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Templates.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.Trim().ToLower();
                query = query.Where(t => t.Name.ToLower().Contains(lowerSearch) ||
                                         (t.Description != null && t.Description.ToLower().Contains(lowerSearch)));
            }

            var templates = await query
                .OrderBy(t => t.Name)
                .Select(t => new TemplateDto(t))
                .ToListAsync(ct);

            return Results.Ok(templates);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template == null) return Results.NotFound("Template not found.");

            return Results.Ok(new TemplateDto(template));
        });

        group.MapPost("/", async (CreateTemplateRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Template name is required.");
            if (string.IsNullOrWhiteSpace(req.Schema))
                return Results.BadRequest("Template schema JSON is required.");

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Description = req.Description?.Trim(),
                Width = req.Width > 0 ? req.Width : 1080,
                Height = req.Height > 0 ? req.Height : 1080,
                Schema = req.Schema,
                ThumbnailMediaAssetId = req.ThumbnailMediaAssetId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Templates.Add(template);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/templates/{template.Id}", new TemplateDto(template));
        }).RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, UpdateTemplateRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Template name is required.");
            if (string.IsNullOrWhiteSpace(req.Schema))
                return Results.BadRequest("Template schema JSON is required.");

            var template = await db.Templates.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template == null) return Results.NotFound("Template not found.");

            template.Name = req.Name.Trim();
            template.Description = req.Description?.Trim();
            template.Width = req.Width > 0 ? req.Width : 1080;
            template.Height = req.Height > 0 ? req.Height : 1080;
            template.Schema = req.Schema;
            template.ThumbnailMediaAssetId = req.ThumbnailMediaAssetId;
            template.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new TemplateDto(template));
        }).RequireAuthorization();

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var template = await db.Templates.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template == null) return Results.NotFound("Template not found.");

            db.Templates.Remove(template);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/assets", async (
            IFormFile file,
            MediaService mediaService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            var mimeType = file.ContentType.ToLowerInvariant();
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif", "image/svg+xml", "image/bmp", "image/x-bmp" };
            if (!allowedMimeTypes.Contains(mimeType))
            {
                return Results.BadRequest($"Unsupported image format '{mimeType}'. Supported formats: PNG, JPG, WEBP, GIF, SVG");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var asset = await mediaService.UploadTemplateAssetAsync(stream, file.FileName, mimeType, ct);
                return Results.Ok(new
                {
                    id = asset.Id,
                    url = $"/api/media/{asset.Id}",
                    fileName = asset.FileName,
                    mimeType = asset.MimeType,
                    width = asset.Width,
                    height = asset.Height,
                    sizeBytes = asset.SizeBytes
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to upload template asset: {ex.Message}");
            }
        }).RequireAuthorization().DisableAntiforgery();

        group.MapPost("/{id:guid}/render", async (
            Guid id,
            RenderTemplateTestRequest? req,
            AppDbContext db,
            MediaService mediaService,
            IImageEngineClient imageEngine,
            CancellationToken ct) =>
        {
            var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template == null) return Results.NotFound("Template not found.");

            var slots = req?.Slots ?? TemplateGeneratorService.GenerateSampleSlots(template.Schema);
            var images = new Dictionary<string, (System.IO.Stream Stream, string FileName, string MimeType)>();
            var disposables = new List<IDisposable>();

            try
            {
                await TemplateAssetResolver.ResolveTemplateImagesAsync(template.Schema, slots, images, disposables, mediaService, ct);
                var pngBytes = await imageEngine.RenderTemplateAsync(template.Schema, slots, images, ct);
                return Results.File(pngBytes, "image/png");
            }
            finally
            {
                foreach (var d in disposables) d.Dispose();
            }
        });

        group.MapPost("/preview", async (
            PreviewTemplateApiRequest req,
            MediaService mediaService,
            IImageEngineClient imageEngine,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Schema))
                return Results.BadRequest("Template schema is required.");

            var slots = req.Slots ?? TemplateGeneratorService.GenerateSampleSlots(req.Schema);
            var images = new Dictionary<string, (System.IO.Stream Stream, string FileName, string MimeType)>();
            var disposables = new List<IDisposable>();

            try
            {
                await TemplateAssetResolver.ResolveTemplateImagesAsync(req.Schema, slots, images, disposables, mediaService, ct);
                var pngBytes = await imageEngine.RenderTemplateAsync(req.Schema, slots, images, ct);
                return Results.File(pngBytes, "image/png");
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Render preview failed: {ex.Message}");
            }
            finally
            {
                foreach (var d in disposables) d.Dispose();
            }
        });

        group.MapPost("/preview-with-images", async (
            HttpRequest httpRequest,
            MediaService mediaService,
            IImageEngineClient imageEngine,
            CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
                return Results.BadRequest("multipart/form-data required");

            var form = await httpRequest.ReadFormAsync(ct);
            var schema = form["schema"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(schema))
                return Results.BadRequest("Template schema is required.");

            var slots = new Dictionary<string, string>();
            foreach (var key in form.Keys.Where(k => k.StartsWith("slots[") && k.EndsWith(']')))
            {
                var slotName = key[6..^1];
                slots[slotName] = form[key].FirstOrDefault() ?? "";
            }

            var images = new Dictionary<string, (System.IO.Stream Stream, string FileName, string MimeType)>();
            var disposables = new List<IDisposable>();

            foreach (var file in form.Files)
            {
                if (file.Name.StartsWith("images[") && file.Name.EndsWith(']'))
                {
                    var slotName = file.Name[7..^1];
                    var stream = file.OpenReadStream();
                    disposables.Add(stream);
                    images[slotName] = (stream, file.FileName, file.ContentType);
                }
            }

            try
            {
                await TemplateAssetResolver.ResolveTemplateImagesAsync(schema, slots, images, disposables, mediaService, ct);
                var pngBytes = await imageEngine.RenderTemplateAsync(schema, slots, images, ct);
                return Results.File(pngBytes, "image/png");
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Render preview failed: {ex.Message}");
            }
            finally
            {
                foreach (var d in disposables) d.Dispose();
            }
        }).DisableAntiforgery();

        group.MapPost("/generate", async (
            GenerateTemplateApiRequest req,
            ClaimsPrincipal principal,
            AppDbContext db,
            LlmProviderService providerService,
            TemplateGeneratorService generator,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
                return Results.BadRequest("Prompt is required.");

            var userId = principal.GetUserId();
            AppUser? user = null;
            if (userId.HasValue)
            {
                user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
            }

            var provider = user != null ? await providerService.GetProviderForUserAsync(db, user, ct) : null;
            provider ??= await db.LlmProviders.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);

            if (provider == null)
            {
                return Results.BadRequest("No active LLM provider configured.");
            }

            var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);
            try
            {
                var result = await generator.GenerateTemplateAsync(req.Prompt, credentials, req.Width, req.Height, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to generate template: {ex.Message}");
            }
        }).RequireAuthorization();

        group.MapPost("/restore-defaults", async (AppDbContext db, CancellationToken ct) =>
        {
            var defaultTemplates = TemplateSeedData.GetDefaultTemplates();
            var existingIds = await db.Templates.Select(t => t.Id).ToListAsync(ct);

            var toAdd = defaultTemplates.Where(d => !existingIds.Contains(d.Id)).ToList();
            if (toAdd.Count > 0)
            {
                db.Templates.AddRange(toAdd);
                await db.SaveChangesAsync(ct);
            }

            var allTemplates = await db.Templates.AsNoTracking().OrderBy(t => t.Name).Select(t => new TemplateDto(t)).ToListAsync(ct);
            return Results.Ok(allTemplates);
        }).RequireAuthorization();
    }
}

