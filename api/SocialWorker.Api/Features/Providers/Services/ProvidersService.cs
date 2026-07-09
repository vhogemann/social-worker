using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Providers.Services;

public class ProvidersService
{
    private readonly AppDbContext _db;
    private readonly ModelCapabilityProbe _probe;
    private readonly HttpClient _http;

    public ProvidersService(AppDbContext db, ModelCapabilityProbe probe, HttpClient http)
    {
        _db = db;
        _probe = probe;
        _http = http;
    }

    public async Task<List<ProviderModels.LlmProviderDto>> GetProvidersAsync(CancellationToken ct = default)
    {
        var rawProviders = await _db.LlmProviders
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var providers = new List<ProviderModels.LlmProviderDto>();
        foreach (var p in rawProviders)
        {
            var caps = await _probe.GetCapabilitiesAsync(p);
            providers.Add(new ProviderModels.LlmProviderDto(
                p.Id,
                p.Name,
                p.ProviderType,
                p.BaseUrl,
                !string.IsNullOrEmpty(p.ApiKey),
                p.Model,
                p.IsDefault,
                p.IsActive,
                caps.SupportsVision,
                caps.SupportsTools
            ));
        }

        return providers;
    }

    public async Task<(ProviderModels.LlmProviderDto? Dto, string? Error)> CreateProviderAsync(ProviderModels.CreateProviderRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.ProviderType) ||
            string.IsNullOrWhiteSpace(req.BaseUrl) || string.IsNullOrWhiteSpace(req.Model))
        {
            return (null, "Name, ProviderType, BaseUrl, and Model are required.");
        }

        var type = req.ProviderType;
        if (type != "OpenRouter" && type != "Ollama")
        {
            return (null, "ProviderType must be OpenRouter or Ollama.");
        }

        var nameExists = await _db.LlmProviders.AnyAsync(p => p.Name.ToLower() == req.Name.ToLower(), ct);
        if (nameExists)
        {
            return (null, "A provider with this name already exists.");
        }

        var isFirst = !await _db.LlmProviders.AnyAsync(ct);

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            ProviderType = type,
            BaseUrl = req.BaseUrl,
            ApiKey = req.ApiKey ?? "",
            Model = req.Model,
            IsDefault = isFirst,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LlmProviders.Add(provider);
        await _db.SaveChangesAsync(ct);

        var caps = await _probe.GetCapabilitiesAsync(provider);

        return (new ProviderModels.LlmProviderDto(
            provider.Id,
            provider.Name,
            provider.ProviderType,
            provider.BaseUrl,
            !string.IsNullOrEmpty(provider.ApiKey),
            provider.Model,
            provider.IsDefault,
            provider.IsActive,
            caps.SupportsVision,
            caps.SupportsTools
        ), null);
    }

    public async Task<(ProviderModels.LlmProviderDto? Dto, string? Error, bool IsNotFound)> UpdateProviderAsync(Guid id, ProviderModels.UpdateProviderRequest req, CancellationToken ct = default)
    {
        var provider = await _db.LlmProviders.FindAsync(new object[] { id }, ct);
        if (provider == null)
        {
            return (null, null, true);
        }

        if (req.Name != null)
        {
            var nameExists = await _db.LlmProviders.AnyAsync(p => p.Id != id && p.Name.ToLower() == req.Name.ToLower(), ct);
            if (nameExists)
            {
                return (null, "A provider with this name already exists.", false);
            }
            provider.Name = req.Name;
        }

        if (req.ProviderType != null)
        {
            if (req.ProviderType != "OpenRouter" && req.ProviderType != "Ollama")
            {
                return (null, "ProviderType must be OpenRouter or Ollama.", false);
            }
            provider.ProviderType = req.ProviderType;
        }

        if (req.BaseUrl != null)
        {
            provider.BaseUrl = req.BaseUrl;
        }

        if (req.ApiKey != null)
        {
            provider.ApiKey = req.ApiKey;
        }

        if (req.Model != null)
        {
            provider.Model = req.Model;
        }

        if (req.IsActive.HasValue)
        {
            if (!req.IsActive.Value && provider.IsDefault)
            {
                return (null, "Cannot deactivate the default provider.", false);
            }
            provider.IsActive = req.IsActive.Value;
        }

        if (req.IsDefault.HasValue)
        {
            if (req.IsDefault.Value)
            {
                await _db.LlmProviders
                    .Where(p => p.Id != id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), ct);

                provider.IsDefault = true;
                provider.IsActive = true;
            }
            else if (provider.IsDefault)
            {
                return (null, "Cannot unset this provider as default directly. Set another provider as default instead.", false);
            }
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var caps = await _probe.GetCapabilitiesAsync(provider);

        return (new ProviderModels.LlmProviderDto(
            provider.Id,
            provider.Name,
            provider.ProviderType,
            provider.BaseUrl,
            !string.IsNullOrEmpty(provider.ApiKey),
            provider.Model,
            provider.IsDefault,
            provider.IsActive,
            caps.SupportsVision,
            caps.SupportsTools
        ), null, false);
    }

    public async Task<(bool Success, string? Error, bool IsNotFound)> DeleteProviderAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _db.LlmProviders.FindAsync(new object[] { id }, ct);
        if (provider == null)
        {
            return (false, null, true);
        }

        if (provider.IsDefault)
        {
            return (false, "Cannot delete the default provider.", false);
        }

        _db.LlmProviders.Remove(provider);
        await _db.SaveChangesAsync(ct);

        return (true, null, false);
    }

    public async Task<ProviderModels.TestProviderResponse> TestProviderConnectionAsync(ProviderModels.TestProviderRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.BaseUrl) || string.IsNullOrWhiteSpace(req.Model) || string.IsNullOrWhiteSpace(req.ProviderType))
        {
            return new ProviderModels.TestProviderResponse(false, "BaseUrl, Model, and ProviderType are required.");
        }

        try
        {
            var payload = new
            {
                model = req.Model,
                messages = new[]
                {
                    new { role = "user", content = "ping" }
                },
                max_tokens = 5,
                stream = false
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{req.BaseUrl.TrimEnd('/')}/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(req.ApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", req.ApiKey);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var response = await _http.SendAsync(request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return new ProviderModels.TestProviderResponse(true, null);
            }

            var errorContent = await response.Content.ReadAsStringAsync(ct);
            var displayError = $"Status {response.StatusCode}: {errorContent}";
            if (displayError.Length > 200) displayError = displayError[..200] + "...";
            return new ProviderModels.TestProviderResponse(false, displayError);
        }
        catch (Exception ex)
        {
            return new ProviderModels.TestProviderResponse(false, ex.Message);
        }
    }

    public async Task<List<ProviderModels.AvailableProviderDto>> GetAvailableProvidersAsync(CancellationToken ct = default)
    {
        return await _db.LlmProviders
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProviderModels.AvailableProviderDto(
                p.Id,
                p.Name,
                p.ProviderType,
                p.Model
            ))
            .ToListAsync(ct);
    }
}
