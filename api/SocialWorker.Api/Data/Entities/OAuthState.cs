using System;

namespace SocialWorker.Api.Data.Entities;

public class OAuthState
{
    public string Nonce { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
