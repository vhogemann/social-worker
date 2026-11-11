using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Charts.Models;
using SocialWorker.Api.Features.Charts.Services;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record RenderChartDatasetArgs(
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("data")] IReadOnlyList<double> Data,
    [property: JsonPropertyName("backgroundColor")] object? BackgroundColor = null,
    [property: JsonPropertyName("borderColor")] object? BorderColor = null,
    [property: JsonPropertyName("fill")] bool? Fill = null);

public sealed record RenderChartArgs(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels,
    [property: JsonPropertyName("datasets")] IReadOnlyList<RenderChartDatasetArgs> Datasets,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("subtitle")] string? Subtitle = null,
    [property: JsonPropertyName("theme")] string? Theme = "dark",
    [property: JsonPropertyName("aspect_ratio")] string? AspectRatio = "16:9",
    [property: JsonPropertyName("card_style")] bool? CardStyle = true);

public sealed record RenderChartToolResult(
    bool Success,
    Guid? MediaAssetId,
    string? MarkdownTag,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(RenderChartToolResult result) => result.ToDisplayText();
    public string ToDisplayText() => Message;
}

public sealed class RenderChartTool : ChatToolBase<RenderChartArgs, RenderChartToolResult>
{
    private readonly ChartService _chartService;

    public RenderChartTool(ChartService chartService)
    {
        _chartService = chartService;
    }

    public override string Name => "render_chart";
    public override string Description =>
        "Renders statistical or numerical data into a high-DPI social media chart image (Bar, Horizontal Bar, Line, Area, or Doughnut/Pie) and attaches it to the active draft as a media asset. " +
        "Use when the user wants to visualize comparisons, metrics, trends, or stats as an engaging visual graphic instead of a text table. " +
        "Returns a markdown image reference (![Title](media://...)) that you can place into the thread text or segment.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "type": {
              "type": "string",
              "enum": ["bar", "horizontal_bar", "line", "area", "doughnut", "pie"],
              "description": "Chart type. Use 'bar' for vertical comparisons, 'horizontal_bar' for rankings or long label lists, 'line' for continuous time-series trends, 'area' for volume trends, 'doughnut' or 'pie' for percentage distributions."
            },
            "labels": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Category or time period labels along the primary axis (e.g. ['Q1', 'Q2', 'Q3', 'Q4'] or ['Desktop', 'Mobile', 'Tablet'])."
            },
            "datasets": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "label": { "type": "string", "description": "Series name (e.g. 'Revenue', 'Active Users')." },
                  "data": {
                    "type": "array",
                    "items": { "type": "number" },
                    "description": "Numerical values corresponding to the labels array."
                  },
                  "backgroundColor": {
                    "description": "Optional hex color or array of hex colors (e.g. '#6366f1' or ['#6366f1', '#10b981'])."
                  },
                  "borderColor": {
                    "description": "Optional border hex color."
                  },
                  "fill": {
                    "type": "boolean",
                    "description": "Whether to fill area under line charts."
                  }
                },
                "required": ["data"]
              },
              "description": "Data series to plot."
            },
            "title": {
              "type": "string",
              "description": "Chart card header title (e.g. 'Quarterly Revenue Growth')."
            },
            "subtitle": {
              "type": "string",
              "description": "Optional subtitle, measurement unit, or data source note (e.g. 'Values in Millions USD • Source: Q4 Report')."
            },
            "theme": {
              "type": "string",
              "enum": ["dark", "light"],
              "description": "Visual theme for card background and axes. Defaults to 'dark'."
            },
            "aspect_ratio": {
              "type": "string",
              "enum": ["16:9", "1:1", "4:5"],
              "description": "Target social aspect ratio. Use '16:9' for Twitter/X and LinkedIn landscape cards, '1:1' for square feeds, '4:5' for Instagram portrait. Defaults to '16:9'."
            },
            "card_style": {
              "type": "boolean",
              "description": "Whether to wrap the chart inside a styled social card container with headers, borders, and margins. Defaults to true."
            }
          },
          "required": ["type", "labels", "datasets"]
        }
        """).RootElement.Clone();

    public override async Task<RenderChartToolResult> ExecuteAsync(RenderChartArgs args, Models.ToolExecutionContext context)
    {
        if (context.DraftId is null)
        {
            return new RenderChartToolResult(false, null, null, "Error: An active draft is required to render and attach a chart.", "No active draft.");
        }

        if (string.IsNullOrWhiteSpace(args.Type))
        {
            return new RenderChartToolResult(false, null, null, "Error: Chart type is required.", "Chart type is required.");
        }

        if (args.Labels is null || args.Labels.Count == 0)
        {
            return new RenderChartToolResult(false, null, null, "Error: Chart labels array cannot be empty.", "Labels cannot be empty.");
        }

        if (args.Datasets is null || args.Datasets.Count == 0)
        {
            return new RenderChartToolResult(false, null, null, "Error: Chart datasets array cannot be empty.", "Datasets cannot be empty.");
        }

        var domainDatasets = args.Datasets.Select(d => new ChartDataset(
            d.Label,
            d.Data,
            d.BackgroundColor,
            d.BorderColor,
            d.Fill)).ToList();

        var request = new ChartRenderRequest(
            args.Type,
            args.Labels,
            domainDatasets,
            args.Title,
            args.Subtitle,
            args.Theme ?? "dark",
            args.AspectRatio ?? "16:9",
            args.CardStyle ?? true);

        var result = await _chartService.RenderAndAttachChartAsync(
            context.UserId,
            context.DraftId.Value,
            request,
            context.CancellationToken);

        if (!result.Success)
        {
            return new RenderChartToolResult(false, null, null, result.Message, result.Error);
        }

        return new RenderChartToolResult(true, result.MediaAssetId, result.MarkdownTag, result.Message);
    }
}
