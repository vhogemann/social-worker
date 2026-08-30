using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed class BlueskyContentPreparationService
{
    private readonly AppDbContext _db;
    private readonly FileStorageProvider _storage;
    private readonly BlueskyApiClient _apiClient;
    private readonly HttpClient _http;

    public BlueskyContentPreparationService(
        AppDbContext db,
        FileStorageProvider storage,
        BlueskyApiClient apiClient,
        HttpClient http)
    {
        _db = db;
        _storage = storage;
        _apiClient = apiClient;
        _http = http;
    }

    public Task<BlueskyPreparedSegment> PrepareAsync(string segment, string accessJwt, CancellationToken ct)
    {
        var doc = ThreadMarkdownDocument.Parse(segment);
        var segmentDoc = doc.Segments.FirstOrDefault() ?? ThreadSegmentDocument.CreateFromBlocks(Array.Empty<Markdig.Syntax.Block>(), string.Empty);
        return PrepareAsync(segmentDoc, accessJwt, ct);
    }

    public async Task<BlueskyPreparedSegment> PrepareAsync(ThreadSegmentDocument segment, string accessJwt, CancellationToken ct)
    {
        var text = segment.PlainText;
        var imageEmbeds = await BuildImageEmbedsAsync(segment.MediaEmbeds, accessJwt, ct);

        var externalEmbed = await BuildYoutubeExternalEmbedAsync(segment.YouTubeEmbed, accessJwt, ct);
        if (externalEmbed is not null)
        {
            return new BlueskyPreparedSegment(text, externalEmbed);
        }

        if (imageEmbeds.Count > 0)
        {
            return new BlueskyPreparedSegment(text, new BlueskyEmbed { Type = "app.bsky.embed.images", Images = imageEmbeds });
        }

        return new BlueskyPreparedSegment(text, null);
    }

    private async Task<List<BlueskyEmbedImage>> BuildImageEmbedsAsync(IReadOnlyList<MediaEmbedReference> mediaEmbeds, string accessJwt, CancellationToken ct)
    {
        var images = new List<BlueskyEmbedImage>();

        foreach (var mediaRef in mediaEmbeds)
        {
            var markdownAlt = mediaRef.AltText;
            var mediaId = mediaRef.MediaId;

            var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.Id == mediaId, ct);
            if (asset is null)
            {
                continue;
            }

            var fullPath = _storage.GetFullPath(asset.FilePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            var (uploadBytes, uploadMime) = EnsureUnderBlueskyBlobSizeLimit(bytes, asset.MimeType);
            var blobUpload = await _apiClient.UploadBlobAsync(uploadBytes, uploadMime, accessJwt, ct);
            if (!blobUpload.Success || blobUpload.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                continue;
            }

            images.Add(new BlueskyEmbedImage
            {
                Alt = !string.IsNullOrWhiteSpace(asset.AltText) ? asset.AltText : markdownAlt,
                Image = blobUpload.Value
            });
        }

        return images;
    }

    public static (byte[] Bytes, string MimeType) EnsureUnderBlueskyBlobSizeLimit(byte[] originalBytes, string originalMimeType)
    {
        const int maxSizeBytes = 950_000;
        if (originalBytes.Length <= maxSizeBytes)
        {
            return (originalBytes, originalMimeType);
        }

        try
        {
            using var data = SkiaSharp.SKData.CreateCopy(originalBytes);
            using var bitmap = SkiaSharp.SKBitmap.Decode(data);
            if (bitmap == null)
            {
                return (originalBytes, originalMimeType);
            }

            int quality = 85;
            using var ms = new MemoryStream();
            bitmap.Encode(ms, SkiaSharp.SKEncodedImageFormat.Jpeg, quality);

            while (ms.Length > maxSizeBytes && quality > 40)
            {
                quality -= 15;
                ms.SetLength(0);
                bitmap.Encode(ms, SkiaSharp.SKEncodedImageFormat.Jpeg, quality);
            }

            if (ms.Length > maxSizeBytes)
            {
                var newWidth = Math.Max(1, bitmap.Width / 2);
                var newHeight = Math.Max(1, bitmap.Height / 2);
                var sampling = new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear, SkiaSharp.SKMipmapMode.None);
                using var resized = bitmap.Resize(new SkiaSharp.SKImageInfo(newWidth, newHeight), sampling);
                if (resized != null)
                {
                    ms.SetLength(0);
                    resized.Encode(ms, SkiaSharp.SKEncodedImageFormat.Jpeg, 80);
                }
            }

            return (ms.ToArray(), "image/jpeg");
        }
        catch
        {
            return (originalBytes, originalMimeType);
        }
    }


    private async Task<BlueskyEmbed?> BuildYoutubeExternalEmbedAsync(YouTubeEmbedReference? ytEmbed, string accessJwt, CancellationToken ct)
    {
        if (ytEmbed is null)
        {
            return null;
        }

        var url = ytEmbed.Url;
        var title = ytEmbed.Title;
        var external = new BlueskyExternal
        {
            Uri = url,
            Title = string.IsNullOrWhiteSpace(title) ? url : title,
            Description = string.Empty
        };

        try
        {
            var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
            var oEmbedRes = await _http.GetAsync(oEmbedUrl, ct);
            if (!oEmbedRes.IsSuccessStatusCode)
            {
                return new BlueskyEmbed { Type = "app.bsky.embed.external", External = external };
            }

            var oEmbed = await oEmbedRes.Content.ReadFromJsonAsync<YouTubeOEmbedResponse>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(oEmbed?.Title))
            {
                external.Title = oEmbed.Title;
            }

            if (!string.IsNullOrWhiteSpace(oEmbed?.ThumbnailUrl))
            {
                var thumbRes = await _http.GetAsync(oEmbed.ThumbnailUrl, ct);
                if (thumbRes.IsSuccessStatusCode)
                {
                    var thumbBytes = await thumbRes.Content.ReadAsByteArrayAsync(ct);
                    var mimeType = thumbRes.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var (uploadBytes, uploadMime) = EnsureUnderBlueskyBlobSizeLimit(thumbBytes, mimeType);
                    var upload = await _apiClient.UploadBlobAsync(uploadBytes, uploadMime, accessJwt, ct);
                    if (upload.Success && upload.Value.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    {
                        external.Thumb = upload.Value;
                    }

                }
            }
        }
        catch
        {
            // Best-effort metadata enrichment.
        }

        return new BlueskyEmbed { Type = "app.bsky.embed.external", External = external };
    }
}

