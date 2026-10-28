using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Media.Models;
using SocialWorker.Api.Features.Media.Services;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record GenerateImageArgs(
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("aspect_ratio")] string? AspectRatio = "1:1",
    [property: JsonPropertyName("template_id")] string? TemplateId = null,
    [property: JsonPropertyName("template_slots")] Dictionary<string, string>? TemplateSlots = null,
    [property: JsonPropertyName("alt_text")] string? AltText = null,
    [property: JsonPropertyName("model")] string? Model = null);

public sealed record GenerateImageToolResult(
    bool Success,
    Guid? MediaAssetId,
    string? MarkdownTag,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(GenerateImageToolResult result) => result.ToDisplayText();

    public string ToDisplayText() => Message;
}

public sealed class GenerateImageTool : ChatToolBase<GenerateImageArgs, GenerateImageToolResult>
{
    private readonly ImageGenerationService _generationService;

    public GenerateImageTool(ImageGenerationService generationService)
    {
        _generationService = generationService;
    }

    public override string Name => "generate_image";
    public override string Description =>
        "Generates a visual asset using generative AI (Flux/OpenRouter) with optional aspect-ratio framing (1:1, 4:5, 16:9, 9:16) " +
        "and optional graphic template compositing (e.g. quote, announcement, stat highlight), automatically attaching the resulting image to the active draft.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "prompt": {
              "type": "string",
              "description": "Visual scene description to generate (e.g. 'Minimalist server room with glowing neon blue cables, digital art, clean studio lighting')."
            },
            "aspect_ratio": {
              "type": "string",
              "enum": ["1:1", "4:5", "16:9", "9:16"],
              "description": "Target social aspect ratio. Use '1:1' for square feed/Bluesky/Twitter, '4:5' for Instagram portrait, '16:9' for landscape cards, '9:16' for vertical stories. Defaults to '1:1'."
            },
            "template_id": {
              "type": "string",
              "description": "Optional template ID or template name (e.g. 'Quote Card (Square)', 'Announcement Card') to composite vector typography and branding over the AI background artwork."
            },
            "template_slots": {
              "type": "object",
              "description": "Optional slot values for text overlays when template_id is provided (e.g. {'quote_text': 'Simplicity is prerequisite for reliability.', 'author': 'Edsger Dijkstra'}).",
              "additionalProperties": { "type": "string" }
            },
            "alt_text": {
              "type": "string",
              "description": "Optional accessibility description for the generated graphic."
            },
            "model": {
              "type": "string",
              "description": "Optional OpenRouter image model slug. Defaults to 'black-forest-labs/flux-1-schnell'."
            }
          },
          "required": ["prompt"]
        }
        """).RootElement.Clone();

    public override async Task<GenerateImageToolResult> ExecuteAsync(GenerateImageArgs args, Models.ToolExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(args.Prompt))
        {
            return new GenerateImageToolResult(false, null, null, "Error: Prompt is required.", "Prompt is required.");
        }

        if (context.DraftId is null)
        {
            return new GenerateImageToolResult(false, null, null, "Error: An active draft is required to generate and attach images.", "No active draft.");
        }

        try
        {
            var cmd = new GenerateImageCommand(
                context.UserId,
                context.DraftId.Value,
                args.Prompt,
                args.AspectRatio ?? "1:1",
                args.Model,
                args.TemplateId,
                args.TemplateSlots,
                args.AltText);

            var result = await _generationService.GenerateAndAttachAsync(cmd, context.CancellationToken);
            var message = $"Successfully generated visual asset {result.MediaAssetId} and attached to draft: {result.MarkdownTag}";
            return new GenerateImageToolResult(true, result.MediaAssetId, result.MarkdownTag, message);
        }
        catch (Exception ex)
        {
            return new GenerateImageToolResult(false, null, null, $"Failed to generate image: {ex.Message}", ex.Message);
        }
    }
}
