using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record AddImageSourceArgs(string Url, string? AltText);

public sealed class AddImageSourceTool : ChatToolBase<AddImageSourceArgs, string>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AddImageSourceTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "add_image_source";
    public override string Description => "Downloads an image from a URL, processes it, resizes it, saves it as a media asset for this draft, and returns the markdown image tag (e.g. ![alt](media://{guid})).";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "url": {
              "type": "string",
              "description": "The absolute URL of the image to download."
            },
            "altText": {
              "type": "string",
              "description": "Optional alternative text describing the image."
            }
          },
          "required": ["url"]
        }
        """).RootElement.Clone();

    public override async Task<string> ExecuteAsync(AddImageSourceArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!draftId.HasValue)
        {
            return "Error: No draft ID active.";
        }

        if (string.IsNullOrWhiteSpace(args.Url) || !Uri.TryCreate(args.Url, UriKind.Absolute, out _))
        {
            return "Error: A valid absolute image URL is required.";
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (draft == null)
        {
            return "Error: Draft not found or access denied.";
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(args.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return $"Error downloading image: {response.StatusCode} {response.ReasonPhrase}";
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return $"Error: URL content is not an image (Content-Type: '{contentType}').";
            }

            var fileName = Path.GetFileName(new Uri(args.Url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName) || fileName == "/" || !fileName.Contains('.'))
            {
                var ext = contentType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ".jpg"
                };
                fileName = $"downloaded_image{ext}";
            }

            using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            using var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            var uploadResult = await mediaService.UploadMediaAsync(
                userId,
                draftId.Value,
                fileName,
                contentType,
                memoryStream,
                ct
            );

            if (!string.IsNullOrWhiteSpace(args.AltText))
            {
                await mediaService.UpdateMediaAltTextAsync(userId, uploadResult.Id, args.AltText, ct);
            }

            var finalTag = !string.IsNullOrWhiteSpace(args.AltText) 
                ? $"![{args.AltText}](media://{uploadResult.Id})"
                : uploadResult.MarkdownTag;

            return $"Successfully imported image. Markdown tag: {finalTag}";
        }
        catch (Exception ex)
        {
            return $"Error importing image: {ex.Message}";
        }
    }
}
