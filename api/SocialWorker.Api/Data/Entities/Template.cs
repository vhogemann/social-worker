using System;

namespace SocialWorker.Api.Data.Entities;

public sealed class Template
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Width { get; set; } = 1080;
    public int Height { get; set; } = 1080;
    public string Schema { get; set; } = "{}";
    public Guid? ThumbnailMediaAssetId { get; set; }
    public MediaAsset? ThumbnailMediaAsset { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static Template Create(
        Guid userId,
        string name,
        string? description,
        int width,
        int height,
        string schema,
        Guid? thumbnailMediaAssetId = null)
    {
        return new Template
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Width = width > 0 ? width : 1080,
            Height = height > 0 ? height : 1080,
            Schema = schema,
            ThumbnailMediaAssetId = thumbnailMediaAssetId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
