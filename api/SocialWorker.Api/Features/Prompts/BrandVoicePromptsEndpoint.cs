using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Prompts;

public static class BrandVoicePromptsEndpoint
{
    public static void MapBrandVoicePromptsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/brand-prompts").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var prompts = await db.BrandVoicePrompts
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new BrandVoicePromptDto(p.Id, p.Name, p.Body, p.IsDefault, p.CreatedAt, p.UpdatedAt))
                .ToListAsync();

            return Results.Ok(prompts);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var prompt = await db.BrandVoicePrompts
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);

            if (prompt == null) return Results.NotFound("Brand voice prompt not found.");

            return Results.Ok(new BrandVoicePromptDto(prompt.Id, prompt.Name, prompt.Body, prompt.IsDefault, prompt.CreatedAt, prompt.UpdatedAt));
        });

        group.MapPost("/", async (BrandVoicePromptRequest req, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Name is required.");
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest("Prompt body is required.");

            if (req.IsDefault)
            {
                var defaults = await db.BrandVoicePrompts
                    .Where(p => p.UserId == userId.Value && p.IsDefault)
                    .ToListAsync();
                foreach (var d in defaults)
                {
                    d.IsDefault = false;
                    d.UpdatedAt = DateTime.UtcNow;
                }
            }

            var prompt = new BrandVoicePrompt
            {
                UserId = userId.Value,
                Name = req.Name,
                Body = req.Body,
                IsDefault = req.IsDefault
            };

            db.BrandVoicePrompts.Add(prompt);
            await db.SaveChangesAsync();

            return Results.Created($"/api/brand-prompts/{prompt.Id}", new BrandVoicePromptDto(prompt.Id, prompt.Name, prompt.Body, prompt.IsDefault, prompt.CreatedAt, prompt.UpdatedAt));
        });

        group.MapPut("/{id:guid}", async (Guid id, BrandVoicePromptRequest req, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Name is required.");
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest("Prompt body is required.");

            var prompt = await db.BrandVoicePrompts
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);

            if (prompt == null) return Results.NotFound("Brand voice prompt not found.");

            if (req.IsDefault && !prompt.IsDefault)
            {
                var defaults = await db.BrandVoicePrompts
                    .Where(p => p.UserId == userId.Value && p.IsDefault && p.Id != id)
                    .ToListAsync();
                foreach (var d in defaults)
                {
                    d.IsDefault = false;
                    d.UpdatedAt = DateTime.UtcNow;
                }
            }

            prompt.Name = req.Name;
            prompt.Body = req.Body;
            prompt.IsDefault = req.IsDefault;
            prompt.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(new BrandVoicePromptDto(prompt.Id, prompt.Name, prompt.Body, prompt.IsDefault, prompt.CreatedAt, prompt.UpdatedAt));
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var prompt = await db.BrandVoicePrompts
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);

            if (prompt != null)
            {
                db.BrandVoicePrompts.Remove(prompt);
                await db.SaveChangesAsync();
            }

            return Results.Ok();
        });
    }
}

public record BrandVoicePromptRequest(string Name, string Body, bool IsDefault);
public record BrandVoicePromptDto(Guid Id, string Name, string Body, bool IsDefault, DateTime CreatedAt, DateTime UpdatedAt);
