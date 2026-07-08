using System;

namespace SocialWorker.Api.Data.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public AppUser User { get; set; } = null!;
}
