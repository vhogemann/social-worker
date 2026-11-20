using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Diagrams.Models;
using SocialWorker.Api.Features.Diagrams.Services;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record RenderDiagramArgs(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("subtitle")] string? Subtitle = null,
    [property: JsonPropertyName("theme")] string? Theme = "dark",
    [property: JsonPropertyName("aspect_ratio")] string? AspectRatio = "auto",
    [property: JsonPropertyName("card_style")] bool? CardStyle = true);

public sealed record RenderDiagramToolResult(
    bool Success,
    Guid? MediaAssetId,
    string? MarkdownTag,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(RenderDiagramToolResult result) => result.ToDisplayText();
    public string ToDisplayText() => Message;
}

public sealed class RenderDiagramTool : ChatToolBase<RenderDiagramArgs, RenderDiagramToolResult>
{
    private readonly DiagramService _diagramService;

    public RenderDiagramTool(DiagramService diagramService)
    {
        _diagramService = diagramService;
    }

    public override string Name => "render_diagram";
    public override string Description =>
        "Renders Mermaid diagram syntax into a high-DPI social media image and attaches it to the active draft as a media asset. " +
        "Supports flowcharts (flowchart/graph), sequence diagrams (sequenceDiagram), state diagrams (stateDiagram-v2), class diagrams (classDiagram), and ER diagrams (erDiagram). " +
        "Quote node labels containing parentheses or brackets (e.g. A[\"API (v2)\"]). " +
        "Returns a markdown image reference (![Title](media://...)) that you can place into the thread text or segment.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "code": {
              "type": "string",
              "description": "Mermaid diagram definition (flowchart TD/LR, sequenceDiagram, stateDiagram-v2, classDiagram, or erDiagram). Raw Mermaid text without markdown fences."
            },
            "title": {
              "type": "string",
              "description": "Optional diagram card header title (e.g. 'Request Lifecycle')."
            },
            "subtitle": {
              "type": "string",
              "description": "Optional subtitle or caption under the title."
            },
            "theme": {
              "type": "string",
              "enum": [
                "dark", "light",
                "zinc-light", "zinc-dark",
                "tokyo-night", "tokyo-night-storm", "tokyo-night-light",
                "catppuccin-mocha", "catppuccin-latte",
                "nord", "nord-light",
                "dracula",
                "github-light", "github-dark",
                "solarized-light", "solarized-dark",
                "one-dark"
              ],
              "description": "Visual theme for the card and diagram. Defaults to 'dark'."
            },
            "aspect_ratio": {
              "type": "string",
              "enum": ["auto", "16:9", "1:1", "4:5"],
              "description": "Target social aspect ratio. Defaults to 'auto', which selects the best-fitting ratio among 16:9, 1:1, and 4:5 based on the diagram's natural proportions."
            },
            "card_style": {
              "type": "boolean",
              "description": "Whether to wrap the diagram inside a styled social card container with headers, borders, and margins. Defaults to true."
            }
          },
          "required": ["code"]
        }
        """).RootElement.Clone();

    public override async Task<RenderDiagramToolResult> ExecuteAsync(RenderDiagramArgs args, Models.ToolExecutionContext context)
    {
        if (context.DraftId is null)
        {
            return new RenderDiagramToolResult(false, null, null, "Error: An active draft is required to render and attach a diagram.", "No active draft.");
        }

        if (string.IsNullOrWhiteSpace(args.Code))
        {
            return new RenderDiagramToolResult(false, null, null, "Error: Mermaid diagram code is required.", "Code is required.");
        }

        var request = new DiagramRenderRequest(
            args.Code,
            args.Title,
            args.Subtitle,
            args.Theme ?? "dark",
            args.AspectRatio ?? "auto",
            args.CardStyle ?? true);

        var result = await _diagramService.RenderAndAttachDiagramAsync(
            context.UserId,
            context.DraftId.Value,
            request,
            context.CancellationToken);

        if (!result.Success)
        {
            return new RenderDiagramToolResult(false, null, null, result.Message, result.Error);
        }

        return new RenderDiagramToolResult(true, result.MediaAssetId, result.MarkdownTag, result.Message);
    }
}
