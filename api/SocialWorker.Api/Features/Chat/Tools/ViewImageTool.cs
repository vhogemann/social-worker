using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ViewImageArgs(string Id);

public sealed record ViewImageResultItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("image_url")] ViewImageResultImageUrl? ImageUrl);

public sealed record ViewImageResultImageUrl(
    [property: JsonPropertyName("url")] string Url);

public sealed class ViewImageTool : ChatToolBase<ViewImageArgs, List<ViewImageResultItem>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ViewImageTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "view_image";
    public override string Description => "Fetch a specific image for visual inspection. Supports media://{guid}, file://{guid}, plain guid, or a direct http/https image URL (which will be imported first).";
    public override bool RequiresVision => true;

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "string",
              "description": "Image identifier (media://{guid}, file://{guid}, guid) or direct http/https image URL."
            }
          },
          "required": ["id"]
        }
        """).RootElement.Clone();

    public override async Task<List<ViewImageResultItem>> ExecuteAsync(ViewImageArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Id))
        {
            throw new ArgumentException("Image id is required.");
        }

        var imageIdStr = args.Id.Trim();
        var imageId = Guid.Empty;
        var isHttpUrl = Uri.TryCreate(imageIdStr, UriKind.Absolute, out var imageUri) &&
                        (string.Equals(imageUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(imageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

        if (!isHttpUrl)
        {
            if (imageIdStr.StartsWith("media://", StringComparison.OrdinalIgnoreCase))
            {
                imageIdStr = imageIdStr.Substring("media://".Length);
            }
            else if (imageIdStr.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                imageIdStr = imageIdStr.Substring("file://".Length);
            }

            if (!Guid.TryParse(imageIdStr, out imageId))
            {
                throw new ArgumentException("Invalid image identifier format. Use media://{guid}, file://{guid}, guid, or a direct http/https image URL.");
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (isHttpUrl)
        {
            if (!draftId.HasValue)
            {
                throw new ArgumentException("Viewing an image URL requires an active draft context.");
            }

            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            using var client = httpClientFactory.CreateClient();
            var importResult = await mediaService.ImportMediaFromUrlAsync(userId, draftId.Value, imageIdStr, client, ct);
            imageId = importResult.Id;
        }

        var asset = await db.MediaAssets.FirstOrDefaultAsync(m => m.Id == imageId, ct);
        if (asset == null)
        {
            throw new InvalidOperationException($"Image {imageId} not found");
        }

        var owned = await db.Drafts.AnyAsync(d => d.Id == asset.DraftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!owned)
        {
            throw new UnauthorizedAccessException("Access denied to target image");
        }

        var fullPath = Path.Combine("/app/uploads", asset.FilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Image file not found on disk");
        }

        using var original = SKBitmap.Decode(fullPath);
        if (original == null)
        {
            throw new InvalidDataException("Failed to decode bitmap from file");
        }

        int maxDim = 512;
        int newWidth = original.Width;
        int newHeight = original.Height;

        if (newWidth > maxDim || newHeight > maxDim)
        {
            double ratio = Math.Min((double)maxDim / newWidth, (double)maxDim / newHeight);
            newWidth = (int)(newWidth * ratio);
            newHeight = (int)(newHeight * ratio);
        }

        using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium) ?? original;
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        var bytes = data.ToArray();
        var base64 = Convert.ToBase64String(bytes);

        return new List<ViewImageResultItem>
        {
            new ViewImageResultItem("text", $"Image: {asset.FileName} ({asset.Width}x{asset.Height}). Current alt text: {asset.AltText ?? "(none)"}", null),
            new ViewImageResultItem("image_url", null, new ViewImageResultImageUrl($"data:image/jpeg;base64,{base64}"))
        };
    }

    protected override ToolExecutionResult BuildResult(List<ViewImageResultItem> result)
    {
        var extraMessages = new List<OpenAiModels.OpenAiMessage>
        {
            new()
            {
                Role = "tool",
                Content = "Image successfully retrieved and loaded."
            },
            new()
            {
                Role = "user",
                Content = result.ToArray()
            }
        };

        return new ToolExecutionResult(result, extraMessages);
    }
}
