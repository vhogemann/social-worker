using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Diagrams.Models;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Diagrams.Services;

public sealed class DiagramService
{
    private static readonly Regex SlugRegex = new(@"[^a-z0-9\-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MermaidFenceRegex = new(
        @"^```(?:mermaid)?\s*\r?\n([\s\S]*?)\r?\n?```$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IImageEngineClient _imageEngineClient;
    private readonly MediaService _mediaService;
    private readonly ILogger<DiagramService> _logger;

    public DiagramService(
        IImageEngineClient imageEngineClient,
        MediaService mediaService,
        ILogger<DiagramService> logger)
    {
        _imageEngineClient = imageEngineClient;
        _mediaService = mediaService;
        _logger = logger;
    }

    public static string StripMermaidFences(string code)
    {
        var trimmed = code.Trim();
        var match = MermaidFenceRegex.Match(trimmed);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }

    public async Task<DiagramResult> RenderAndAttachDiagramAsync(
        Guid userId,
        Guid draftId,
        DiagramRenderRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return DiagramResult.Fail("Mermaid diagram code is required.");
        }

        var cleanedCode = StripMermaidFences(request.Code);
        if (string.IsNullOrWhiteSpace(cleanedCode))
        {
            return DiagramResult.Fail("Mermaid diagram code cannot be empty.");
        }

        var normalizedRequest = request with
        {
            Code = cleanedCode,
            Theme = string.IsNullOrWhiteSpace(request.Theme) ? "dark" : request.Theme.Trim(),
            AspectRatio = string.IsNullOrWhiteSpace(request.AspectRatio) ? "auto" : request.AspectRatio.Trim(),
        };

        try
        {
            var pngBytes = await _imageEngineClient.RenderDiagramAsync(normalizedRequest, ct);

            var titleOrDefault = string.IsNullOrWhiteSpace(normalizedRequest.Title)
                ? "mermaid"
                : normalizedRequest.Title.Trim();

            var slug = SlugRegex.Replace(titleOrDefault.ToLowerInvariant().Replace(' ', '-'), "");
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = "mermaid";
            }

            var fileName = $"diagram-{slug}-{Guid.NewGuid().ToString("N")[..8]}.png";
            var linkText = string.IsNullOrWhiteSpace(normalizedRequest.Title)
                ? "Diagram"
                : normalizedRequest.Title.Trim();

            var altText = string.IsNullOrWhiteSpace(normalizedRequest.Subtitle)
                ? $"Diagram: {linkText}"
                : $"Diagram: {linkText} - {normalizedRequest.Subtitle.Trim()}";

            using var stream = new MemoryStream(pngBytes);
            var cmd = new UploadMediaCommand(
                userId,
                draftId,
                fileName,
                "image/png",
                stream,
                AltText: altText,
                MarkdownLinkText: linkText);

            var uploadResult = await _mediaService.UploadMediaAsync(cmd, ct);
            var tag = $"![{linkText}](media://{uploadResult.Id})";
            var message = $"Successfully rendered diagram and attached to draft: {tag}";

            return DiagramResult.Ok(uploadResult.Id, tag, message);
        }
        catch (ArgumentException ex)
        {
            return DiagramResult.Fail(ex.Message, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render and store diagram for draft {DraftId}", draftId);
            return DiagramResult.Fail($"Failed to render diagram: {ex.Message}", ex.Message);
        }
    }
}
