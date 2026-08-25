using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ImageEngine;

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

        group.MapPost("/{id:guid}/render", async (
            Guid id,
            RenderTemplateTestRequest? req,
            AppDbContext db,
            IImageEngineClient imageEngine,
            CancellationToken ct) =>
        {
            var template = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template == null) return Results.NotFound("Template not found.");

            var slots = req?.Slots ?? new Dictionary<string, string>();
            var images = new Dictionary<string, (System.IO.Stream Stream, string FileName, string MimeType)>();

            var pngBytes = await imageEngine.RenderTemplateAsync(template.Schema, slots, images, ct);
            return Results.File(pngBytes, "image/png");
        });
    }
}
