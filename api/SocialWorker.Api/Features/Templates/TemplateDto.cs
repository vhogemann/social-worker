using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Templates;

public sealed record TemplateDto(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    int Width,
    int Height,
    string Schema,
    Guid? ThumbnailMediaAssetId,
    string ThumbnailUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public TemplateDto(Template template) : this(
        template.Id,
        template.UserId,
        template.Name,
        template.Description,
        template.Width,
        template.Height,
        template.Schema,
        template.ThumbnailMediaAssetId,
        template.ThumbnailMediaAssetId.HasValue
            ? $"/api/media/{template.ThumbnailMediaAssetId.Value}"
            : $"/api/templates/{template.Id}/render",
        template.CreatedAt,
        template.UpdatedAt)
    {
    }
}

public sealed record CreateTemplateRequest(
    string Name,
    string? Description,
    int Width,
    int Height,
    string Schema,
    Guid? ThumbnailMediaAssetId = null);

public sealed record UpdateTemplateRequest(
    string Name,
    string? Description,
    int Width,
    int Height,
    string Schema,
    Guid? ThumbnailMediaAssetId = null);

public sealed record RenderTemplateTestRequest(
    Dictionary<string, string>? Slots = null);
