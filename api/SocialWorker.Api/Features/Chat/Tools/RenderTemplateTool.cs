using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record RenderTemplateArgs(
    [property: JsonPropertyName("template_id")] string TemplateId,
    [property: JsonPropertyName("slots")] Dictionary<string, string>? Slots = null,
    [property: JsonPropertyName("alt_text")] string? AltText = null);

public sealed record RenderTemplateToolResult(
    bool Success,
    Guid? MediaAssetId,
    string? MarkdownTag,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(RenderTemplateToolResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Message;
    }
}

public sealed class RenderTemplateTool : ChatToolBase<RenderTemplateArgs, RenderTemplateToolResult>
{
    private readonly AppDbContext _db;
    private readonly IImageEngineClient _imageEngine;
    private readonly MediaService _mediaService;

    public RenderTemplateTool(
        AppDbContext db,
        IImageEngineClient imageEngine,
        MediaService mediaService)
    {
        _db = db;
        _imageEngine = imageEngine;
        _mediaService = mediaService;
    }

    public override string Name => "render_template";
    public override string Description =>
        "Renders a graphic template with supplied slot values (text overlays, images, watermarks). " +
        "Automatically attaches the rendered PNG image to the active draft and returns a markdown media link (media://{id}).";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "template_id": {
              "type": "string",
              "description": "The GUID or exact name of the template to render."
            },
            "slots": {
              "type": "object",
              "description": "Key-value mapping of slot names to text values or media references (e.g. 'quote_text': 'Hello world', 'background_image': 'media://019f...').",
              "additionalProperties": { "type": "string" }
            },
            "alt_text": {
              "type": "string",
              "description": "Optional alt text description for the rendered image."
            }
          },
          "required": ["template_id"]
        }
        """).RootElement.Clone();

    public override async Task<RenderTemplateToolResult> ExecuteAsync(RenderTemplateArgs args, Models.ToolExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(args.TemplateId))
        {
            return new RenderTemplateToolResult(false, null, null, "Error: Template ID or name is required.", "Template ID or name is required.");
        }

        if (!context.DraftId.HasValue)
        {
            return new RenderTemplateToolResult(false, null, null, "Error: An active draft is required to render and attach templates.", "No active draft.");
        }

        var draft = await _db.Drafts.FirstOrDefaultAsync(
            d => d.Id == context.DraftId.Value && d.UserId == context.UserId && d.Status != DraftStatus.Deleted,
            context.CancellationToken);

        if (draft == null)
        {
            return new RenderTemplateToolResult(false, null, null, "Error: Active draft not found or access denied.", "Draft not found.");
        }

        Template? template = null;
        if (Guid.TryParse(args.TemplateId.Trim(), out var templateGuid))
        {
            template = await _db.Templates.FirstOrDefaultAsync(t => t.Id == templateGuid, context.CancellationToken);
        }

        if (template == null)
        {
            var searchName = args.TemplateId.Trim().ToLower();
            template = await _db.Templates.FirstOrDefaultAsync(t => t.Name.ToLower() == searchName, context.CancellationToken);
        }

        if (template == null)
        {
            return new RenderTemplateToolResult(false, null, null, $"Error: Template '{args.TemplateId}' not found.", "Template not found.");
        }

        var inputSlots = args.Slots ?? new Dictionary<string, string>();
        var resolvedSlots = new Dictionary<string, string>();
        var images = new Dictionary<string, (Stream Stream, string FileName, string MimeType)>();
        var streamsToDispose = new List<Stream>();

        try
        {
            foreach (var (slotKey, slotValue) in inputSlots)
            {
                if (string.IsNullOrWhiteSpace(slotValue)) continue;

                var trimmedVal = slotValue.Trim();
                var isMediaRef = false;
                var mediaId = Guid.Empty;

                if (trimmedVal.StartsWith("media://", StringComparison.OrdinalIgnoreCase))
                {
                    isMediaRef = Guid.TryParse(trimmedVal.Substring("media://".Length).Trim(), out mediaId);
                }
                else if (trimmedVal.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    isMediaRef = Guid.TryParse(trimmedVal.Substring("file://".Length).Trim(), out mediaId);
                }
                else if (Guid.TryParse(trimmedVal, out mediaId))
                {
                    isMediaRef = await _db.MediaAssets.AnyAsync(m => m.Id == mediaId, context.CancellationToken);
                }

                if (isMediaRef && mediaId != Guid.Empty)
                {
                    try
                    {
                        var (fullPath, mimeType) = await _mediaService.GetMediaFileAsync(mediaId, context.CancellationToken);
                        var fileStream = File.OpenRead(fullPath);
                        streamsToDispose.Add(fileStream);

                        images[slotKey] = (fileStream, Path.GetFileName(fullPath), mimeType);
                        resolvedSlots[slotKey] = slotKey;
                    }
                    catch (Exception ex)
                    {
                        return new RenderTemplateToolResult(false, null, null, $"Failed to load image asset {mediaId} for slot '{slotKey}': {ex.Message}", ex.Message);
                    }
                }
                else
                {
                    resolvedSlots[slotKey] = slotValue;
                }
            }

            var renderedBytes = await _imageEngine.RenderTemplateAsync(
                template.Schema,
                resolvedSlots,
                images,
                context.CancellationToken);

            var uploadCmd = new UploadMediaCommand(
                context.UserId,
                draft.Id,
                $"{template.Name.ToLower().Replace(" ", "-")}-{DateTime.UtcNow:yyyyMMddHHmmss}.png",
                "image/png",
                new MemoryStream(renderedBytes),
                args.AltText ?? $"{template.Name} rendered image",
                $"{template.Name} rendered");

            var uploadResult = await _mediaService.UploadMediaAsync(uploadCmd, context.CancellationToken);

            if (!template.ThumbnailMediaAssetId.HasValue)
            {
                template.ThumbnailMediaAssetId = uploadResult.Id;
                template.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(context.CancellationToken);
            }

            var message = $"Successfully rendered template '{template.Name}' and attached image:\n{uploadResult.MarkdownTag}";
            return new RenderTemplateToolResult(true, uploadResult.Id, uploadResult.MarkdownTag, message);
        }
        catch (Exception ex)
        {
            return new RenderTemplateToolResult(false, null, null, $"Failed to render template: {ex.Message}", ex.Message);
        }
        finally
        {
            foreach (var stream in streamsToDispose)
            {
                try
                {
                    stream.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
