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
    public int? ContextWindowTokens { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static LlmProvider Create(Features.Providers.ProviderModels.CreateProviderRequest req, bool isDefault = false)
        => Create(req.Name, req.ProviderType, req.BaseUrl, req.Model, req.ApiKey, req.ContextWindowTokens, isDefault);

    public static LlmProvider Create(
        string name,
        string providerType,
        string baseUrl,
        string model,
        string? apiKey = null,
        int? contextWindowTokens = null,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Provider name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model name is required.", nameof(model));
        if (providerType != "OpenRouter" && providerType != "Ollama")
            throw new ArgumentException("ProviderType must be OpenRouter or Ollama.", nameof(providerType));

        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ProviderType = providerType,
            BaseUrl = baseUrl.TrimEnd('/'),
            ApiKey = apiKey?.Trim() ?? "",
            Model = model.Trim(),
            ContextWindowTokens = contextWindowTokens,
            IsDefault = isDefault,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
