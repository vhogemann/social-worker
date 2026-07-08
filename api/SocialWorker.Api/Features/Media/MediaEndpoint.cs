using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SkiaSharp;

namespace SocialWorker.Api.Features.Media;

public static class MediaEndpoint
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/drafts/{draftId:guid}/media", async (ClaimsPrincipal principal, AppDbContext db, Guid draftId, IFormFile file) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (draft is null) return Results.NotFound();

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded");
            }

            var mimeType = file.ContentType.ToLowerInvariant();
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" };
            if (!allowedMimeTypes.Contains(mimeType))
            {
                return Results.BadRequest("Unsupported image format. Supported formats: JPG, PNG, WEBP, GIF");
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
            {
                ext = mimeType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ".jpg"
                };
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

                var existing = await db.MediaAssets.FirstOrDefaultAsync(m => m.Sha256 == shaHashStr);
                if (existing != null)
                {
                    var sharedAsset = new MediaAsset
                    {
                        Id = Guid.NewGuid(),
                        DraftId = draftId,
                        FileName = file.FileName,
                        MimeType = existing.MimeType,
                        FilePath = existing.FilePath,
                        Sha256 = shaHashStr,
                        SizeBytes = existing.SizeBytes,
                        Width = existing.Width,
                        Height = existing.Height
                    };

                    db.MediaAssets.Add(sharedAsset);
                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        id = sharedAsset.Id,
                        markdownTag = $"![{sharedAsset.FileName}](media://{sharedAsset.Id})"
                    });
                }

                var mediaId = Guid.NewGuid();
                var relativePath = Path.Combine(draftId.ToString(), $"{mediaId}{ext}");
                var uploadDir = Path.Combine("/app/uploads", draftId.ToString());

                Directory.CreateDirectory(uploadDir);
                var fullPath = Path.Combine("/app/uploads", relativePath);

                using var stream = file.OpenReadStream();
                using var codec = SKCodec.Create(stream);
                if (codec == null)
                {
                    return Results.BadRequest("Invalid image file");
                }

                int originalWidth = codec.Info.Width;
                int originalHeight = codec.Info.Height;
                int finalWidth = originalWidth;
                int finalHeight = originalHeight;
                long finalSize = file.Length;

                stream.Position = 0;

                const int maxDimension = 1200;
                if (originalWidth > maxDimension || originalHeight > maxDimension)
                {
                    double ratio = Math.Min((double)maxDimension / originalWidth, (double)maxDimension / originalHeight);
                    finalWidth = (int)(originalWidth * ratio);
                    finalHeight = (int)(originalHeight * ratio);

                    using var original = SKBitmap.Decode(codec);
                    using var resized = original.Resize(new SKImageInfo(finalWidth, finalHeight), SKFilterQuality.High);
                    using var image = SKImage.FromBitmap(resized);

                    var format = mimeType.Contains("png") ? SKEncodedImageFormat.Png :
                                 mimeType.Contains("webp") ? SKEncodedImageFormat.Webp :
                                 mimeType.Contains("gif") ? SKEncodedImageFormat.Gif :
                                 SKEncodedImageFormat.Jpeg;

                    using var data = image.Encode(format, 85);
                    var resizedBytes = data.ToArray();
                    await File.WriteAllBytesAsync(fullPath, resizedBytes);
                    finalSize = resizedBytes.Length;
                }
                else
                {
                    using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fs);
                }

                var mediaAsset = new MediaAsset
                {
                    Id = mediaId,
                    DraftId = draftId,
                    FileName = file.FileName,
                    MimeType = mimeType,
                    FilePath = relativePath,
                    Sha256 = shaHashStr,
                    SizeBytes = finalSize,
                    Width = finalWidth,
                    Height = finalHeight
                };

                db.MediaAssets.Add(mediaAsset);
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    id = mediaAsset.Id,
                    markdownTag = $"![{mediaAsset.FileName}](media://{mediaAsset.Id})"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to upload image: {ex.Message}");
            }
        }).RequireAuthorization().DisableAntiforgery();

        app.MapGet("/api/media/{id:guid}", async (AppDbContext db, Guid id) =>
        {
            var asset = await db.MediaAssets.FindAsync(id);
            if (asset == null) return Results.NotFound();

            var fullPath = Path.Combine("/app/uploads", asset.FilePath);
            if (!File.Exists(fullPath)) return Results.NotFound();

            return Results.File(fullPath, asset.MimeType);
        });

        app.MapPatch("/api/media/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, UpdateMediaRequest req) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var asset = await db.MediaAssets.Include(m => m.Draft).FirstOrDefaultAsync(m => m.Id == id);
            if (asset == null) return Results.NotFound();

            if (asset.Draft.UserId != userId.Value)
            {
                return Results.Forbid();
            }

            if (req.AltText is not null)
            {
                asset.AltText = req.AltText;
            }

            await db.SaveChangesAsync();
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
        }).RequireAuthorization();

        app.MapDelete("/api/media/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var asset = await db.MediaAssets.Include(m => m.Draft).FirstOrDefaultAsync(m => m.Id == id);
            if (asset == null) return Results.NotFound();

            if (asset.Draft.UserId != userId.Value)
            {
                return Results.Forbid();
            }

            var isShared = await db.MediaAssets.AnyAsync(m => m.Id != asset.Id && m.FilePath == asset.FilePath);
            if (!isShared)
            {
                var fullPath = Path.Combine("/app/uploads", asset.FilePath);
                if (File.Exists(fullPath))
                {
                    try { File.Delete(fullPath); } catch {}
                }

                var dir = Path.GetDirectoryName(fullPath);
                if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    try { Directory.Delete(dir); } catch {}
                }
            }

            db.MediaAssets.Remove(asset);
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).RequireAuthorization();
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}

public sealed record UpdateMediaRequest(string? AltText);
