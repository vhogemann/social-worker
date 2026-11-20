using System;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Diagrams.Models;

public sealed record DiagramRenderRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonPropertyName("subtitle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subtitle = null,
    [property: JsonPropertyName("theme")] string Theme = "dark",
    [property: JsonPropertyName("aspectRatio")] string AspectRatio = "auto",
    [property: JsonPropertyName("cardStyle")] bool CardStyle = true);

public sealed record DiagramResult(
    bool Success,
    Guid? MediaAssetId,
    string? MarkdownTag,
    string Message,
    string? Error = null)
{
    public static DiagramResult Ok(Guid mediaAssetId, string markdownTag, string message) =>
        new(true, mediaAssetId, markdownTag, message);

    public static DiagramResult Fail(string message, string? error = null) =>
        new(false, null, null, message, error ?? message);
}
