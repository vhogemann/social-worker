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
using SocialWorker.Api.Features.Templates.Services;

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
        "Renders a graphic card template with supplied slot values (quotes, metrics, titles, and background images). " +
        "Automatically attaches the rendered PNG image to the active draft and returns a markdown media link tag (e.g. ![Alt](media://{guid})). " +
        "After calling this tool, insert the returned markdown tag into your draft using replace_editor_content.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "template_id": {
              "type": "string",
              "description": "The exact GUID or exact Name of the template to render (from list_templates, e.g. 'Quote Card (Portrait)', 'Stat Highlight', 'Code Snippet Card')."
            },
            "slots": {
              "type": "object",
              "description": "Key-value mapping of slot names to text values or media references. For images, pass media://<guid> (from add_image_source) or file://<guid>. (e.g. {'quote_text': 'Simplicity is prerequisite for reliability.', 'author': 'Dijkstra', 'background_image': 'media://4f3...'})",
              "additionalProperties": { "type": "string" }
            },
            "alt_text": {
              "type": "string",
              "description": "Optional descriptive alt text for accessibility and markdown preview."
            }
          },
          "required": ["template_id", "slots"]
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
            template = await _db.Templates.FirstOrDefaultAsync(t => t.Id == templateGuid && t.UserId == context.UserId, context.CancellationToken);
        }

        if (template == null)
        {
            var searchName = args.TemplateId.Trim().ToLower();
            template = await _db.Templates.FirstOrDefaultAsync(t => t.Name.ToLower() == searchName && t.UserId == context.UserId, context.CancellationToken);
        }

        if (template == null)
        {
            return new RenderTemplateToolResult(false, null, null, $"Error: Template '{args.TemplateId}' not found.", "Template not found.");
        }

        var rawInputSlots = args.Slots ?? new Dictionary<string, string>();
        var inputSlots = NormalizeInputSlots(rawInputSlots, template.Schema);
        var resolvedSlots = new Dictionary<string, string>();
        var images = new Dictionary<string, (Stream Stream, string FileName, string MimeType)>();
        var streamsToDispose = new List<IDisposable>();

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

            await TemplateAssetResolver.ResolveTemplateImagesAsync(
                template.Schema,
                resolvedSlots,
                images,
                streamsToDispose,
                _mediaService,
                context.CancellationToken);

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

    private static Dictionary<string, string> NormalizeInputSlots(
        Dictionary<string, string> inputSlots,
        string schemaJson)
    {
        var normalized = new Dictionary<string, string>(inputSlots, StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
            {
                var canonicalSlots = new List<string>();
                foreach (var s in slotsEl.EnumerateArray())
                {
                    if (s.TryGetProperty("name", out var n) && n.GetString() is { } name && !string.IsNullOrWhiteSpace(name))
                    {
                        canonicalSlots.Add(name);
                    }
                }

                foreach (var canonical in canonicalSlots)
                {
                    if (normalized.ContainsKey(canonical))
                        continue;

                    var cleanCanonical = canonical.Replace("_", "").Replace("-", "");
                    var foundKey = normalized.Keys.FirstOrDefault(k =>
                        k.Replace("_", "").Replace("-", "").Equals(cleanCanonical, StringComparison.OrdinalIgnoreCase));

                    if (foundKey == null)
                    {
                        if (canonical.Equals("quote_text", StringComparison.OrdinalIgnoreCase) || canonical.Equals("quote", StringComparison.OrdinalIgnoreCase))
                        {
                            foundKey = normalized.Keys.FirstOrDefault(k => k.Equals("quote", StringComparison.OrdinalIgnoreCase) || k.Equals("quote_text", StringComparison.OrdinalIgnoreCase) || k.Equals("text", StringComparison.OrdinalIgnoreCase));
                        }
                        else if (canonical.Equals("background_image", StringComparison.OrdinalIgnoreCase) || canonical.Equals("bg", StringComparison.OrdinalIgnoreCase))
                        {
                            foundKey = normalized.Keys.FirstOrDefault(k => k.Equals("image", StringComparison.OrdinalIgnoreCase) || k.Equals("background_image", StringComparison.OrdinalIgnoreCase) || k.Equals("bg", StringComparison.OrdinalIgnoreCase) || k.Equals("background", StringComparison.OrdinalIgnoreCase));
                        }
                        else if (canonical.Equals("headline", StringComparison.OrdinalIgnoreCase) || canonical.Equals("slide_title", StringComparison.OrdinalIgnoreCase))
                        {
                            foundKey = normalized.Keys.FirstOrDefault(k => k.Equals("title", StringComparison.OrdinalIgnoreCase) || k.Equals("headline", StringComparison.OrdinalIgnoreCase) || k.Equals("header", StringComparison.OrdinalIgnoreCase));
                        }
                        else if (canonical.Equals("body_text", StringComparison.OrdinalIgnoreCase) || canonical.Equals("slide_body", StringComparison.OrdinalIgnoreCase))
                        {
                            foundKey = normalized.Keys.FirstOrDefault(k => k.Equals("body", StringComparison.OrdinalIgnoreCase) || k.Equals("body_text", StringComparison.OrdinalIgnoreCase) || k.Equals("content", StringComparison.OrdinalIgnoreCase) || k.Equals("description", StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    if (foundKey != null && normalized.TryGetValue(foundKey, out var val))
                    {
                        normalized[canonical] = val;
                    }
                }
            }
        }
        catch
        {
        }

        return normalized;
    }
}
