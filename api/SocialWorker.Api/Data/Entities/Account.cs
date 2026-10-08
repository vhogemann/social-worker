using System;

namespace SocialWorker.Api.Data.Entities;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    
    public string Platform { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string CredentialsEncrypted { get; set; } = string.Empty;
    public DateTime? TokenExpiresAt { get; set; }
    
    public string Status { get; set; } = "Active"; // Active, Invalid
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
