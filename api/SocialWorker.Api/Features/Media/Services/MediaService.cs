using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Media;

public sealed record MediaAssetDto(
    Guid Id,
    Guid DraftId,
    string FileName,
    string MimeType,
    string? AltText,
    string FilePath,
    long SizeBytes,
    int Width,
    int Height
);

public sealed record UploadMediaResult(Guid Id, string MarkdownTag);

public sealed class MediaService
{
    private readonly AppDbContext _db;
    private readonly ImageResizer _resizer;
    private readonly FileStorageProvider _storage;

    public MediaService(AppDbContext db, ImageResizer resizer, FileStorageProvider storage)
    {
        _db = db;
        _resizer = resizer;
        _storage = storage;
    }

    public async Task<UploadMediaResult> UploadMediaAsync(
        Guid userId,
        Guid draftId,
        string fileName,
        string mimeType,
        Stream stream,
        CancellationToken ct,
        string? altText = null)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var imageBytes = buffer.ToArray();

        string shaHashStr;
        using (var hashStream = new MemoryStream(imageBytes))
        {
            var hashBytes = sha256.ComputeHash(hashStream);
            shaHashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        stream = new MemoryStream(imageBytes);

        var existing = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Sha256 == shaHashStr, ct);
        if (existing != null)
        {
            var sharedAsset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                DraftId = draftId,
                FileName = fileName,
                MimeType = existing.MimeType,
                FilePath = existing.FilePath,
                Sha256 = shaHashStr,
                SizeBytes = existing.SizeBytes,
                Width = existing.Width,
                Height = existing.Height
            };

            if (!string.IsNullOrEmpty(altText))
                sharedAsset.AltText = altText;
            
            _db.MediaAssets.Add(sharedAsset);
            await _db.SaveChangesAsync(ct);

            var displayAlt = altText ?? sharedAsset.FileName;
            return new UploadMediaResult(sharedAsset.Id, $"![{displayAlt}](media://{sharedAsset.Id})");
        }

        var mediaId = Guid.NewGuid();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
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

        var relativePath = Path.Combine(draftId.ToString(), $"{mediaId}{ext}");

        stream.Position = 0;
        var processResult = _resizer.ProcessImage(stream, mimeType);

        await _storage.WriteFileAsync(relativePath, processResult.Data);

        var mediaAsset = new MediaAsset
        {
            Id = mediaId,
            DraftId = draftId,
            FileName = fileName,
            MimeType = mimeType,
            FilePath = relativePath,
            Sha256 = shaHashStr,
            SizeBytes = processResult.Data.Length,
            Width = processResult.Width,
            Height = processResult.Height,
            AltText = altText
        };

        _db.MediaAssets.Add(mediaAsset);
        await _db.SaveChangesAsync(ct);

        var markdownAlt = altText ?? mediaAsset.FileName;
        return new UploadMediaResult(mediaAsset.Id, $"![{markdownAlt}](media://{mediaAsset.Id})");
    }

    public async Task<(string FullPath, string MimeType)> GetMediaFileAsync(Guid id, CancellationToken ct)
    {
        var asset = await _db.MediaAssets.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Media asset not found.");

        if (!_storage.FileExists(asset.FilePath))
        {
            throw new FileNotFoundException("Physical media file not found on disk.");
        }

        return (_storage.GetFullPath(asset.FilePath), asset.MimeType);
    }

    public async Task<MediaAssetDto> UpdateMediaAltTextAsync(Guid userId, Guid id, string? altText, CancellationToken ct)
    {
        var asset = await _db.MediaAssets.Include(m => m.Draft).FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Media asset not found.");

        if (asset.Draft.UserId != userId)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        asset.AltText = altText;
        await _db.SaveChangesAsync(ct);

        return new MediaAssetDto(
            asset.Id,
            asset.DraftId,
            asset.FileName,
            asset.MimeType,
            asset.AltText,
            asset.FilePath,
            asset.SizeBytes,
            asset.Width,
            asset.Height
        );
    }

    public async Task DeleteMediaAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var asset = await _db.MediaAssets.Include(m => m.Draft).FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Media asset not found.");

        if (asset.Draft.UserId != userId)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var isShared = await _db.MediaAssets.AnyAsync(m => m.Id != asset.Id && m.FilePath == asset.FilePath, ct);
        if (!isShared)
        {
            _storage.DeleteFileAndEmptyFolder(asset.FilePath);
        }

        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UploadMediaResult> ImportMediaFromUrlAsync(
        Guid userId,
        Guid draftId,
        string url,
        HttpClient httpClient,
        CancellationToken ct,
        string? altText = null)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("A valid absolute image URL is required.", nameof(url));
        }

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to download image: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"URL content is not an image (Content-Type: '{mimeType}').");
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName == "/" || !fileName.Contains('.'))
        {
            var ext = mimeType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };
            fileName = $"downloaded_image{ext}";
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await UploadMediaAsync(userId, draftId, fileName, mimeType, stream, ct, altText);
    }
}
