using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media.Models;
using SocialWorker.Api.Features.Templates.Services;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Media.Services;

public sealed class ImageGenerationService
{
    private readonly IImageGenerationClient _imageClient;
    private readonly IImageEngineClient _imageEngine;
    private readonly MediaService _mediaService;
    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly IConfiguration _config;
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(
        IImageGenerationClient imageClient,
        IImageEngineClient imageEngine,
        MediaService mediaService,
        AppDbContext db,
        LlmProviderService providerService,
        IConfiguration config,
        ILogger<ImageGenerationService> logger)
    {
        _imageClient = imageClient;
        _imageEngine = imageEngine;
        _mediaService = mediaService;
        _db = db;
        _providerService = providerService;
        _config = config;
        _logger = logger;
    }

    public async Task<GeneratedImageResult> GenerateAndAttachAsync(
        GenerateImageCommand command,
        CancellationToken ct = default)
    {
        var apiKey = await ResolveApiKeyAsync(command.UserId, ct);
        var rawBytes = await _imageClient.GenerateImageAsync(
            command.Prompt,
            command.Model,
            command.AspectRatio,
            apiKey,
            ct);

        byte[] finalBytes = rawBytes;
        int finalWidth = 1080;
        int finalHeight = 1080;

        if (!string.IsNullOrWhiteSpace(command.TemplateId))
        {
            finalBytes = await CompositeWithTemplateAsync(command, rawBytes, ct);
        }
        else
        {
            try
            {
                using var rawStream = new MemoryStream(rawBytes);
                var formatted = await _imageEngine.FormatAspectRatioAsync(
                    rawStream,
                    "image/png",
                    command.AspectRatio ?? "1:1",
                    "blur_padding",
                    ct);

                finalBytes = formatted.Data;
                finalWidth = formatted.Width;
                finalHeight = formatted.Height;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply aspect-ratio formatting via image-engine. Using raw bytes.");
            }
        }

        var fileName = $"ai-gen-{Guid.NewGuid():N}.png";
        using var uploadStream = new MemoryStream(finalBytes);
        var uploadRes = await _mediaService.UploadMediaAsync(
            new UploadMediaCommand(
                command.UserId,
                command.DraftId,
                fileName,
                "image/png",
                uploadStream,
                command.AltText ?? command.Prompt,
                command.Prompt),
            ct);

        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == uploadRes.Id, ct);
        if (asset != null)
        {
            finalWidth = asset.Width > 0 ? asset.Width : finalWidth;
            finalHeight = asset.Height > 0 ? asset.Height : finalHeight;
        }

        return new GeneratedImageResult(
            uploadRes.Id,
            uploadRes.MarkdownTag,
            $"/api/media/{uploadRes.Id}",
            finalWidth,
            finalHeight,
            "image/png");
    }

    private async Task<byte[]> CompositeWithTemplateAsync(
        GenerateImageCommand command,
        byte[] rawImageBytes,
        CancellationToken ct)
    {
        Template? template = null;
        if (Guid.TryParse(command.TemplateId, out var templateGuid))
        {
            template = await _db.Templates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateGuid && t.UserId == command.UserId, ct);
        }

        if (template == null)
        {
            var searchName = command.TemplateId!.Trim().ToLowerInvariant();
            template = await _db.Templates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name.ToLower() == searchName && t.UserId == command.UserId, ct);
        }

        if (template == null)
        {
            _logger.LogWarning("Template '{TemplateId}' not found. Skipping template compositing.", command.TemplateId);
            return rawImageBytes;
        }

        var slots = command.TemplateSlots != null
            ? new Dictionary<string, string>(command.TemplateSlots, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var imageSlotName = FindImageSlotName(template.Schema) ?? "background_image";

        var images = new Dictionary<string, (Stream Stream, string FileName, string MimeType)>();
        var disposables = new List<IDisposable>();

        var bgStream = new MemoryStream(rawImageBytes);
        disposables.Add(bgStream);
        images[imageSlotName] = (bgStream, "ai_background.png", "image/png");

        try
        {
            await TemplateAssetResolver.ResolveTemplateImagesAsync(template.Schema, slots, images, disposables, _mediaService, ct);
            return await _imageEngine.RenderTemplateAsync(template.Schema, slots, images, ct);
        }
        finally
        {
            foreach (var d in disposables) d.Dispose();
        }
    }

    private static string? FindImageSlotName(string schemaJson)
    {
        try
        {
            var node = JsonNode.Parse(schemaJson);
            var slots = node?["slots"]?.AsArray();
            if (slots != null)
            {
                foreach (var s in slots)
                {
                    if (s == null) continue;
                    var type = s["type"]?.GetValue<string>();
                    var name = s["name"]?.GetValue<string>();
                    if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private async Task<string?> ResolveApiKeyAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user != null)
        {
            var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
            if (provider != null && !string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                return provider.ApiKey;
            }
        }

        var defaultProvider = await _db.LlmProviders
            .FirstOrDefaultAsync(p => p.ProviderType == "OpenRouter" && p.IsActive, ct);

        if (defaultProvider != null && !string.IsNullOrWhiteSpace(defaultProvider.ApiKey))
        {
            return defaultProvider.ApiKey;
        }

        return _config["LLM__ApiKey"];
    }
}
