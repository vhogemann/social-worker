using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class Source
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;
    public SourceKind Kind { get; set; }
    public string Reference { get; set; } = "";
    public string? Content { get; set; }
    public string? Title { get; set; }
    public string? Sha256 { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
