using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class PlatformThread
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;
    public string Platform { get; set; } = "";
    public PlatformThreadStage Stage { get; set; } = PlatformThreadStage.Draft;
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
