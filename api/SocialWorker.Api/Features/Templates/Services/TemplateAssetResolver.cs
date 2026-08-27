using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Templates.Services;

public static class TemplateAssetResolver
{
    public static async Task ResolveTemplateImagesAsync(
        string schemaJson,
        IDictionary<string, string>? slots,
        IDictionary<string, (Stream Stream, string FileName, string MimeType)> images,
        IList<IDisposable> disposables,
        MediaService mediaService,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(schemaJson))
        {
            try
            {
                var doc = JsonNode.Parse(schemaJson);
                var layers = doc?["layers"]?.AsArray();
                if (layers != null)
                {
                    foreach (var layer in layers)
                    {
                        if (layer == null) continue;
                        var type = layer["type"]?.GetValue<string>();
                        if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase))
                        {
                            var src = layer["src"]?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(src))
                            {
                                await TryAttachMediaAssetAsync(src, src, images, disposables, mediaService, ct);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback gracefully on schema parse errors
            }
        }

        if (slots != null)
        {
            foreach (var kvp in slots)
            {
                var slotKey = kvp.Key;
                var slotValue = kvp.Value;
                if (!string.IsNullOrWhiteSpace(slotValue) && !images.ContainsKey(slotKey))
                {
                    await TryAttachMediaAssetAsync(slotValue, slotKey, images, disposables, mediaService, ct);
                }
            }
        }
    }

    private static async Task TryAttachMediaAssetAsync(
        string reference,
        string imageKey,
        IDictionary<string, (Stream Stream, string FileName, string MimeType)> images,
        IList<IDisposable> disposables,
        MediaService mediaService,
        CancellationToken ct)
    {
        if (images.ContainsKey(imageKey)) return;

        var mediaId = ExtractMediaId(reference);
        if (!mediaId.HasValue || mediaId.Value == Guid.Empty) return;

        try
        {
            var (fullPath, mimeType) = await mediaService.GetMediaFileAsync(mediaId.Value, ct);
            if (File.Exists(fullPath))
            {
                var stream = File.OpenRead(fullPath);
                disposables.Add(stream);
                images[imageKey] = (stream, Path.GetFileName(fullPath), mimeType);
            }
        }
        catch
        {
            // Ignore missing or inaccessible media asset
        }
    }

    public static Guid? ExtractMediaId(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;

        var trimmed = reference.Trim();

        if (trimmed.StartsWith("/api/media/", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed.Substring("/api/media/".Length).TrimEnd('/');
            if (Guid.TryParse(idPart, out var id)) return id;
        }

        if (trimmed.StartsWith("media://", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed.Substring("media://".Length).TrimEnd('/');
            if (Guid.TryParse(idPart, out var id)) return id;
        }

        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed.Substring("file://".Length).TrimEnd('/');
            if (Guid.TryParse(idPart, out var id)) return id;
        }

        if (Guid.TryParse(trimmed, out var directId))
        {
            return directId;
        }

        return null;
    }
}
