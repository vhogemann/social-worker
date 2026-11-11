using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Charts.Models;

public sealed record ChartDataset(
    [property: JsonPropertyName("label"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label,
    [property: JsonPropertyName("data")] IReadOnlyList<double> Data,
    [property: JsonPropertyName("backgroundColor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? BackgroundColor = null,
    [property: JsonPropertyName("borderColor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? BorderColor = null,
    [property: JsonPropertyName("fill"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Fill = null);

public sealed record ChartRenderRequest(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels,
    [property: JsonPropertyName("datasets")] IReadOnlyList<ChartDataset> Datasets,
    [property: JsonPropertyName("title"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonPropertyName("subtitle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subtitle = null,
    [property: JsonPropertyName("theme")] string Theme = "dark",
    [property: JsonPropertyName("aspectRatio")] string AspectRatio = "16:9",
    [property: JsonPropertyName("cardStyle")] bool CardStyle = true);

public sealed record ChartResult(
    bool Success,
    Guid? MediaAssetId,
    string? MarkdownTag,
    string Message,
    string? Error = null);
