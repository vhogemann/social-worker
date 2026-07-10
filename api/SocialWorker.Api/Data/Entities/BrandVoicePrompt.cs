using System;

namespace SocialWorker.Api.Data.Entities;

public class BrandVoicePrompt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
