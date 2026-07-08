using System;

namespace SocialWorker.Api.Data.Entities;

public class MediaAsset
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string? AltText { get; set; }
    public string FilePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
