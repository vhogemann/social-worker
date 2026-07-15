using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed class BlueskyContentPreparationService
{
    private static readonly Regex YoutubeMarkdownRegex = SharedPatterns.YoutubeMarkdownRegex;

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

    public async Task<BlueskyPreparedSegment> PrepareAsync(string segment, string accessJwt, CancellationToken ct)
    {
        var text = segment.Trim();
        var imageEmbeds = await BuildImageEmbedsAsync(text, accessJwt, ct);
        text = SharedPatterns.StripMediaMarkdown(text).Trim();

        var externalEmbed = await BuildYoutubeExternalEmbedAsync(text, accessJwt, ct);
        if (externalEmbed is not null)
        {
            text = YoutubeMarkdownRegex.Replace(text, string.Empty).Trim();
            return new BlueskyPreparedSegment(text, externalEmbed);
        }

        if (imageEmbeds.Count > 0)
        {
            return new BlueskyPreparedSegment(text, new BlueskyEmbed { Type = "app.bsky.embed.images", Images = imageEmbeds });
        }

        return new BlueskyPreparedSegment(text, null);
    }

    private async Task<List<BlueskyEmbedImage>> BuildImageEmbedsAsync(string text, string accessJwt, CancellationToken ct)
    {
        var images = new List<BlueskyEmbedImage>();

        foreach (var mediaRef in SharedPatterns.ExtractMediaReferences(text))
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
            var blobUpload = await _apiClient.UploadBlobAsync(bytes, asset.MimeType, accessJwt, ct);
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

    private async Task<BlueskyEmbed?> BuildYoutubeExternalEmbedAsync(string text, string accessJwt, CancellationToken ct)
    {
        var match = YoutubeMarkdownRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups[1].Value;
        var url = match.Groups[2].Value;
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
                    var upload = await _apiClient.UploadBlobAsync(thumbBytes, mimeType, accessJwt, ct);
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
