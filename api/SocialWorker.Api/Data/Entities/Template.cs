using System;

namespace SocialWorker.Api.Data.Entities;

public sealed class Template
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Width { get; set; } = 1080;
    public int Height { get; set; } = 1080;
    public string Schema { get; set; } = "{}";
    public Guid? ThumbnailMediaAssetId { get; set; }
    public MediaAsset? ThumbnailMediaAsset { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
