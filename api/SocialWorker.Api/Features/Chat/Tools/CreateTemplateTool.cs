using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Templates.Services;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record CreateTemplateArgs(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("width")] int? Width = null,
    [property: JsonPropertyName("height")] int? Height = null,
    [property: JsonPropertyName("test_slots")] Dictionary<string, string>? TestSlots = null);

public sealed record CreateTemplateToolResult(
    bool Success,
    Guid? TemplateId,
    string? MarkdownTag,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(CreateTemplateToolResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Message;
    }
}

public sealed class CreateTemplateTool : ChatToolBase<CreateTemplateArgs, CreateTemplateToolResult>
{
    private readonly AppDbContext _db;
    private readonly IImageEngineClient _imageEngine;
    private readonly MediaService _mediaService;

    public CreateTemplateTool(
        AppDbContext db,
        IImageEngineClient imageEngine,
        MediaService mediaService)
    {
        _db = db;
        _imageEngine = imageEngine;
        _mediaService = mediaService;
    }

    public override string Name => "create_template";
    public override string Description =>
        "Creates and saves a new graphic template into the system template library by specifying its name, canvas dimensions, slots, and layer definitions. " +
        "Automatically validates rendering with image-engine before saving and returns an attached test-preview image.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "name": {
              "type": "string",
              "description": "The display name for the template (e.g. 'Cyber Alert Card', 'Minimal Quote')."
            },
            "description": {
              "type": "string",
              "description": "Optional description of the visual layout and intended social post use-case."
            },
            "width": {
              "type": "integer",
              "description": "Canvas width in pixels (typically 1080)."
            },
            "height": {
              "type": "integer",
              "description": "Canvas height in pixels (1080 for square, 1350 for 4:5 portrait)."
            },
            "schema": {
              "type": "string",
              "description": "The full declarative JSON schema definition containing 'name', 'width', 'height', 'slots' (array of {name, type, required, description}), and 'layers' (ordered array of {type: 'rect'|'circle'|'image'|'text', fill, stroke, strokeWidth, borderRadius, x, y, w, h, slot, fontSize, fontFamily, fontWeight, color, align, maxWidth, lineHeight, prefix, suffix, textTransform, fit})."
            },
            "test_slots": {
              "type": "object",
              "description": "Optional sample slot values to render the initial preview image (e.g. {'quote_text': 'Design is intelligence made visible.'}).",
              "additionalProperties": { "type": "string" }
            }
          },
          "required": ["name", "schema"]
        }
        """).RootElement.Clone();

    public override async Task<CreateTemplateToolResult> ExecuteAsync(CreateTemplateArgs args, Models.ToolExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(args.Name))
        {
            return new CreateTemplateToolResult(false, null, null, "Error: Template name is required.", "Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(args.Schema))
        {
            return new CreateTemplateToolResult(false, null, null, "Error: Template schema JSON is required.", "Template schema is required.");
        }

        // Test render against image-engine
        byte[] pngBytes;
        try
        {
            var slots = args.TestSlots ?? TemplateGeneratorService.GenerateSampleSlots(args.Schema);
            var images = new Dictionary<string, (Stream Stream, string FileName, string MimeType)>();
            pngBytes = await _imageEngine.RenderTemplateAsync(args.Schema, slots, images, context.CancellationToken);
        }
        catch (Exception ex)
        {
            return new CreateTemplateToolResult(false, null, null, $"Error: Failed to render and validate template schema: {ex.Message}", ex.Message);
        }

        var width = args.Width is > 0 ? args.Width.Value : 1080;
        var height = args.Height is > 0 ? args.Height.Value : 1080;

        var template = Template.Create(
            context.UserId,
            args.Name,
            args.Description,
            width,
            height,
            args.Schema);

        string? markdownTag = null;

        // If active draft exists, upload sample preview and link thumbnail
        if (context.DraftId.HasValue)
        {
            try
            {
                var uploadCmd = new UploadMediaCommand(
                    context.UserId,
                    context.DraftId.Value,
                    $"{template.Name.ToLower().Replace(" ", "-")}-preview.png",
                    "image/png",
                    new MemoryStream(pngBytes),
                    $"{template.Name} preview",
                    $"{template.Name} preview");

                var uploadResult = await _mediaService.UploadMediaAsync(uploadCmd, context.CancellationToken);
                markdownTag = uploadResult.MarkdownTag;
                template.ThumbnailMediaAssetId = uploadResult.Id;
            }
            catch
            {
                // Best effort thumbnail attachment
            }
        }

        _db.Templates.Add(template);
        await _db.SaveChangesAsync(context.CancellationToken);

        var previewMsg = !string.IsNullOrEmpty(markdownTag)
            ? $"\n\nPreview:\n{markdownTag}"
            : "";

        var message = $"Successfully created and saved template '{template.Name}' (ID: {template.Id}, {template.Width}x{template.Height}).{previewMsg}";
        return new CreateTemplateToolResult(true, template.Id, markdownTag, message);
    }
}
