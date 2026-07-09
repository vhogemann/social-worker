using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Sources;

public static class SourcesEndpoint
{
    public static void MapSourcesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts/{draftId:guid}").RequireAuthorization();

        group.MapGet("/sources", async (ClaimsPrincipal principal, AppDbContext db, Guid draftId) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draftExists = await db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (!draftExists) return Results.NotFound();

            var sources = await db.Sources
                .Where(s => s.DraftId == draftId)
                .Select(s => new
                {
                    s.Id,
                    s.DraftId,
                    Kind = s.Kind.ToString(),
                    s.Reference,
                    s.Title,
                    s.AddedAt
                })
                .ToListAsync();

            return Results.Ok(sources);
        });

        group.MapPost("/files", async (ClaimsPrincipal principal, AppDbContext db, SourceExtractor extractor, Guid draftId, IFormFile file) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (draft is null) return Results.NotFound();

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded");
            }

            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                byte[] hashBytes;
                using (var tempStream = new MemoryStream())
                {
                    using var uploadStream = file.OpenReadStream();
                    await uploadStream.CopyToAsync(tempStream);
                    tempStream.Position = 0;
                    hashBytes = sha256.ComputeHash(tempStream);
                }
                var shaHashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                var existing = await db.Sources.FirstOrDefaultAsync(s => s.Sha256 == shaHashStr);
                if (existing != null)
                {
                    var source = new Source
                    {
                        DraftId = draftId,
                        Kind = SourceKind.File,
                        Reference = file.FileName,
                        Title = existing.Title ?? file.FileName,
                        Content = existing.Content,
                        Sha256 = shaHashStr,
                        AddedAt = DateTime.UtcNow
                    };

                    db.Sources.Add(source);
                    draft.Status = DraftStatus.Editing;
                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        sourceId = source.Id,
                        markdownLink = $"[File: {source.Reference}](file://{source.Id})"
                    });
                }

                draft.Status = DraftStatus.Sourcing;
                await db.SaveChangesAsync();

                string extractedText;
                try
                {
                    extractedText = await extractor.ExtractTextAsync(file);
                }
                catch (NotSupportedException ex)
                {
                    draft.Status = DraftStatus.Editing;
                    await db.SaveChangesAsync();
                    return Results.BadRequest(ex.Message);
                }

                var newSource = new Source
                {
                    DraftId = draftId,
                    Kind = SourceKind.File,
                    Reference = file.FileName,
                    Title = file.FileName,
                    Content = extractedText,
                    Sha256 = shaHashStr,
                    AddedAt = DateTime.UtcNow
                };

                db.Sources.Add(newSource);
                draft.Status = DraftStatus.Editing;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    sourceId = newSource.Id,
                    markdownLink = $"[File: {newSource.Reference}](file://{newSource.Id})"
                });
            }
            catch (Exception ex)
            {
                draft.Status = DraftStatus.Editing;
                await db.SaveChangesAsync();
                return Results.BadRequest($"Failed to process file attachment: {ex.Message}");
            }
        }).DisableAntiforgery();
    }

    public static async Task ReconcileSourcesAsync(AppDbContext db, IServiceScopeFactory scopeFactory, Draft draft, string content)
    {
        if (scopeFactory == null)
        {
            var service = new SourcesService(db, null!, null!);
            await service.ReconcileSourcesAsync(draft, content);
        }
        else
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SourcesService>();
            await service.ReconcileSourcesAsync(draft, content);
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}
