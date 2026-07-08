using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Providers;

public static class ProvidersEndpoint
{
    public static void MapProvidersEndpoints(this WebApplication app)
    {
        var adminGroup = app.MapGroup("/api/providers")
            .RequireAuthorization("Admin");

        adminGroup.MapGet("/", async (AppDbContext db, ModelCapabilityProbe probe, CancellationToken ct) =>
        {
            var rawProviders = await db.LlmProviders
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            var providers = new System.Collections.Generic.List<ProviderModels.LlmProviderDto>();
            foreach (var p in rawProviders)
            {
                var caps = await probe.GetCapabilitiesAsync(p);
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

            return Results.Ok(providers);
        });

        adminGroup.MapPost("/", async (AppDbContext db, ModelCapabilityProbe probe, ProviderModels.CreateProviderRequest req, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.ProviderType) ||
                string.IsNullOrWhiteSpace(req.BaseUrl) || string.IsNullOrWhiteSpace(req.Model))
            {
                return Results.BadRequest("Name, ProviderType, BaseUrl, and Model are required.");
            }

            var type = req.ProviderType;
            if (type != "OpenRouter" && type != "Ollama")
            {
                return Results.BadRequest("ProviderType must be OpenRouter or Ollama.");
            }

            var nameExists = await db.LlmProviders.AnyAsync(p => p.Name.ToLower() == req.Name.ToLower(), ct);
            if (nameExists)
            {
                return Results.Conflict("A provider with this name already exists.");
            }

            // If there are no providers, this one must be default
            var isFirst = !await db.LlmProviders.AnyAsync(ct);

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

            db.LlmProviders.Add(provider);
            await db.SaveChangesAsync(ct);

            var caps = await probe.GetCapabilitiesAsync(provider);

            return Results.Ok(new ProviderModels.LlmProviderDto(
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
            ));
        });

        adminGroup.MapPatch("/{id:guid}", async (AppDbContext db, ModelCapabilityProbe probe, Guid id, ProviderModels.UpdateProviderRequest req, CancellationToken ct) =>
        {
            var provider = await db.LlmProviders.FindAsync(new object[] { id }, ct);
            if (provider == null)
            {
                return Results.NotFound();
            }

            if (req.Name != null)
            {
                var nameExists = await db.LlmProviders.AnyAsync(p => p.Id != id && p.Name.ToLower() == req.Name.ToLower(), ct);
                if (nameExists)
                {
                    return Results.Conflict("A provider with this name already exists.");
                }
                provider.Name = req.Name;
            }

            if (req.ProviderType != null)
            {
                if (req.ProviderType != "OpenRouter" && req.ProviderType != "Ollama")
                {
                    return Results.BadRequest("ProviderType must be OpenRouter or Ollama.");
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
                    return Results.BadRequest("Cannot deactivate the default provider.");
                }
                provider.IsActive = req.IsActive.Value;
            }

            if (req.IsDefault.HasValue)
            {
                if (req.IsDefault.Value)
                {
                    // Set all others to false
                    await db.LlmProviders
                        .Where(p => p.Id != id)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), ct);

                    provider.IsDefault = true;
                    provider.IsActive = true; // Default must be active
                }
                else if (provider.IsDefault)
                {
                    return Results.BadRequest("Cannot unset this provider as default directly. Set another provider as default instead.");
                }
            }

            provider.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var caps = await probe.GetCapabilitiesAsync(provider);

            return Results.Ok(new ProviderModels.LlmProviderDto(
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
            ));
        });

        adminGroup.MapDelete("/{id:guid}", async (AppDbContext db, Guid id, CancellationToken ct) =>
        {
            var provider = await db.LlmProviders.FindAsync(new object[] { id }, ct);
            if (provider == null)
            {
                return Results.NotFound();
            }

            if (provider.IsDefault)
            {
                return Results.BadRequest("Cannot delete the default provider.");
            }

            db.LlmProviders.Remove(provider);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        adminGroup.MapPost("/test", async (ProviderModels.TestProviderRequest req, HttpClient http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.BaseUrl) || string.IsNullOrWhiteSpace(req.Model) || string.IsNullOrWhiteSpace(req.ProviderType))
            {
                return Results.Ok(new ProviderModels.TestProviderResponse(false, "BaseUrl, Model, and ProviderType are required."));
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

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, $"{req.BaseUrl.TrimEnd('/')}/chat/completions")
                {
                    Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(req.ApiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", req.ApiKey);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(8));

                var response = await http.SendAsync(request, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return Results.Ok(new ProviderModels.TestProviderResponse(true, null));
                }

                var errorContent = await response.Content.ReadAsStringAsync(ct);
                var displayError = $"Status {response.StatusCode}: {errorContent}";
                if (displayError.Length > 200) displayError = displayError[..200] + "...";
                return Results.Ok(new ProviderModels.TestProviderResponse(false, displayError));
            }
            catch (Exception ex)
            {
                return Results.Ok(new ProviderModels.TestProviderResponse(false, ex.Message));
            }
        });

        app.MapGet("/api/providers/available", async (AppDbContext db, CancellationToken ct) =>
        {
            var providers = await db.LlmProviders
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new ProviderModels.AvailableProviderDto(
                    p.Id,
                    p.Name,
                    p.ProviderType,
                    p.Model
                ))
                .ToListAsync(ct);

            return Results.Ok(providers);
        }).RequireAuthorization();
    }
}
