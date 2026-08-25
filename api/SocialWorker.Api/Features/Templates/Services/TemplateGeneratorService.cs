using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.ImageEngine;

namespace SocialWorker.Api.Features.Templates.Services;

public sealed class TemplateGeneratorService
{
    private readonly ILlmProviderAdapter _adapter;
    private readonly IImageEngineClient _imageEngine;
    private readonly ILogger<TemplateGeneratorService> _logger;

    public TemplateGeneratorService(
        ILlmProviderAdapter adapter,
        IImageEngineClient imageEngine,
        ILogger<TemplateGeneratorService> logger)
    {
        _adapter = adapter;
        _imageEngine = imageEngine;
        _logger = logger;
    }

    public async Task<GeneratedTemplateResponse> GenerateTemplateAsync(
        string userPrompt,
        LlmCredentials credentials,
        int? targetWidth = null,
        int? targetHeight = null,
        CancellationToken ct = default)
    {
        var width = targetWidth is > 0 ? targetWidth.Value : 1080;
        var height = targetHeight is > 0 ? targetHeight.Value : 1080;

        var systemPrompt = $$"""
        You are an expert graphic designer and visual template engineer.
        Your task is to generate a declarative 2D image template JSON definition for a canvas renderer (@napi-rs/canvas).
        Canvas size: {{width}}x{{height}}.

        The output MUST be a valid JSON object conforming to this exact structure:
        {
          "name": "Concise Display Name",
          "description": "Clear description of visual style and intended use",
          "width": {{width}},
          "height": {{height}},
          "slots": [
            { "name": "slot_key", "type": "text" | "image" | "color", "required": true | false, "description": "...", "defaultValue": "..." }
          ],
          "layers": [
            // Supported layer types:
            // 1. rect: { "type": "rect", "fill": "#hex" or "rgba(...)", "stroke": "#hex", "strokeWidth": 2, "x": 0, "y": 0, "w": "100%" or 1000, "h": "100%" or 1000, "borderRadius": 16, "opacity": 1.0 }
            // 2. circle: { "type": "circle", "fill": "#hex", "cx": "50%" or 500, "cy": "50%" or 500, "radius": 100, "opacity": 1.0 }
            // 3. image: { "type": "image", "slot": "slot_key", "fit": "cover" | "contain" | "fill", "x": 0, "y": 0, "w": "100%", "h": "100%", "borderRadius": 0 }
            // 4. text: { "type": "text", "slot": "slot_key", "text": "fallback text", "x": 100, "y": 100, "maxWidth": 880, "fontSize": 44, "fontFamily": "Inter, sans-serif", "fontWeight": "bold" or "normal", "color": "#ffffff", "align": "left" | "center" | "right", "lineHeight": 1.35, "prefix": "", "suffix": "", "textTransform": "uppercase" | "lowercase" | "none" }
          ]
        }

        Rules:
        1. Always include high-contrast background and text layers so text is easily legible.
        2. Position text layers with appropriate margins (e.g. x: 80-100) and maxWidth (e.g. width - 2*margin) to prevent text clipping.
        3. Define useful customizable slots for text headings, body, quotes, metrics, or optional background images.
        4. Return ONLY the valid JSON object inside ```json ... ``` code fences. No other conversational text.
        """;

        var request = new OpenAiModels.ChatCompletionRequest
        {
            Model = credentials.Model,
            Messages = new List<OpenAiModels.OpenAiMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = $"Design a template based on this request: {userPrompt}" }
            },
            Stream = false
        };

        var response = await _adapter.CompleteAsync(request, credentials, ct);
        var rawContent = response?.Choices?.FirstOrDefault()?.Message?.Content?.ToString() ?? "";
        var schemaJson = ExtractJson(rawContent);

        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            throw new InvalidOperationException("Failed to generate a valid JSON template schema.");
        }

        // Test render with synthesized sample slots
        byte[]? pngBytes = null;
        string? renderError = null;

        try
        {
            var testSlots = GenerateSampleSlots(schemaJson);
            var emptyImages = new Dictionary<string, (System.IO.Stream Stream, string FileName, string MimeType)>();
            pngBytes = await _imageEngine.RenderTemplateAsync(schemaJson, testSlots, emptyImages, ct);
        }
        catch (Exception ex)
        {
            renderError = ex.Message;
            _logger.LogWarning(ex, "Initial template render test failed, attempting 1-shot auto-repair");
        }

        // 1-Shot LLM Auto-Repair if initial render failed
        if (pngBytes == null && !string.IsNullOrEmpty(renderError))
        {
            var repairRequest = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = $"Design a template based on this request: {userPrompt}" },
                    new() { Role = "assistant", Content = schemaJson },
                    new() { Role = "user", Content = $"The schema failed rendering in the image engine with error:\n{renderError}\n\nPlease fix the layer tree and return the corrected JSON object inside ```json ``` fences." }
                },
                Stream = false
            };

            var repairResponse = await _adapter.CompleteAsync(repairRequest, credentials, ct);
            var repairedRaw = repairResponse?.Choices?.FirstOrDefault()?.Message?.Content?.ToString() ?? "";
            var repairedSchema = ExtractJson(repairedRaw);

            if (!string.IsNullOrWhiteSpace(repairedSchema))
            {
                schemaJson = repairedSchema;
                var testSlots = GenerateSampleSlots(schemaJson);
                var emptyImages = new Dictionary<string, (System.IO.Stream Stream, string FileName, string MimeType)>();
                pngBytes = await _imageEngine.RenderTemplateAsync(schemaJson, testSlots, emptyImages, ct);
            }
        }

        var (name, description, extractedWidth, extractedHeight) = ParseTemplateMetadata(schemaJson, width, height);

        var previewBase64 = pngBytes != null ? Convert.ToBase64String(pngBytes) : null;

        return new GeneratedTemplateResponse(
            name,
            description,
            extractedWidth,
            extractedHeight,
            schemaJson,
            previewBase64);
    }

    public static Dictionary<string, string> GenerateSampleSlots(string schemaJson)
    {
        var slots = new Dictionary<string, string>();

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var slotEl in slotsEl.EnumerateArray())
                {
                    var name = slotEl.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var type = slotEl.TryGetProperty("type", out var t) ? t.GetString() : "text";
                    var defaultVal = slotEl.TryGetProperty("defaultValue", out var d) ? d.GetString() : null;

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (!string.IsNullOrEmpty(defaultVal))
                    {
                        slots[name] = defaultVal;
                    }
                    else if (type == "text")
                    {
                        slots[name] = name.Contains("quote", StringComparison.OrdinalIgnoreCase) ? "Simplicity is prerequisite for reliability."
                            : name.Contains("author", StringComparison.OrdinalIgnoreCase) ? "Edsger W. Dijkstra"
                            : name.Contains("metric", StringComparison.OrdinalIgnoreCase) || name.Contains("stat", StringComparison.OrdinalIgnoreCase) || name.Contains("value", StringComparison.OrdinalIgnoreCase) ? "94.8%"
                            : name.Contains("title", StringComparison.OrdinalIgnoreCase) || name.Contains("headline", StringComparison.OrdinalIgnoreCase) ? "Sample Headline Title"
                            : name.Contains("body", StringComparison.OrdinalIgnoreCase) || name.Contains("description", StringComparison.OrdinalIgnoreCase) ? "This is a sample description text showing how your rendered template looks."
                            : $"Sample {name}";
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors for sample extraction
        }

        return slots;
    }

    private static (string Name, string? Description, int Width, int Height) ParseTemplateMetadata(string schemaJson, int defaultWidth, int defaultHeight)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Custom Template" : "Custom Template";
            var desc = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            var width = root.TryGetProperty("width", out var w) && w.TryGetInt32(out var widthVal) ? widthVal : defaultWidth;
            var height = root.TryGetProperty("height", out var h) && h.TryGetInt32(out var heightVal) ? heightVal : defaultHeight;

            return (name, desc, width, height);
        }
        catch
        {
            return ("Custom Template", null, defaultWidth, defaultHeight);
        }
    }

    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var fenceMatch = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            return fenceMatch.Groups[1].Value.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text.Substring(start, end - start + 1).Trim();
        }

        return text.Trim();
    }
}
