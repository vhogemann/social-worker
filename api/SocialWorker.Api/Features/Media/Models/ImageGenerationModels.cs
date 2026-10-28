using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Media.Models;

public sealed record OpenRouterImageRequest(
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("aspect_ratio"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AspectRatio = null,
    [property: JsonPropertyName("n")] int N = 1);

public sealed record OpenRouterImageResponse(
    [property: JsonPropertyName("data")] OpenRouterImageData[]? Data);

public sealed record OpenRouterImageData(
    [property: JsonPropertyName("b64_json")] string? B64Json,
    [property: JsonPropertyName("url")] string? Url);

public sealed record GenerateImageRequest(
    Guid DraftId,
    string Prompt,
    string? AspectRatio = "1:1",
    string? Model = null,
    string? TemplateId = null,
    Dictionary<string, string>? TemplateSlots = null,
    string? AltText = null)
{
    public GenerateImageCommand ToCommand(Guid userId) => new(
        userId,
        DraftId,
        Prompt,
        AspectRatio ?? "1:1",
        Model,
        TemplateId,
        TemplateSlots,
        AltText);
}

public sealed record GenerateImageCommand(
    Guid UserId,
    Guid DraftId,
    string Prompt,
    string AspectRatio = "1:1",
    string? Model = null,
    string? TemplateId = null,
    Dictionary<string, string>? TemplateSlots = null,
    string? AltText = null);

public sealed record GeneratedImageResult(
    Guid MediaAssetId,
    string MarkdownTag,
    string Url,
    int Width,
    int Height,
    string MimeType);
