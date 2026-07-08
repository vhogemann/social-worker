using System;

namespace SocialWorker.Api.Data.Entities;

public class LlmProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ProviderType { get; set; } = ""; // "OpenRouter" | "Ollama"
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
