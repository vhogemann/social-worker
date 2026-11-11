using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Charts.Models;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Charts.Services;

public sealed class ChartService
{
    private static readonly Regex SlugRegex = new(@"[^a-z0-9\-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly IImageEngineClient _imageEngineClient;
    private readonly MediaService _mediaService;
    private readonly ILogger<ChartService> _logger;

    public ChartService(
        IImageEngineClient imageEngineClient,
        MediaService mediaService,
        ILogger<ChartService> logger)
    {
        _imageEngineClient = imageEngineClient;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<ChartResult> RenderAndAttachChartAsync(
        Guid userId,
        Guid draftId,
        ChartRenderRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return new ChartResult(false, null, null, "Chart type is required.", "Chart type is required.");
        }

        if (request.Labels == null || request.Labels.Count == 0)
        {
            return new ChartResult(false, null, null, "Chart labels cannot be empty.", "Labels cannot be empty.");
        }

        if (request.Datasets == null || request.Datasets.Count == 0)
        {
            return new ChartResult(false, null, null, "Chart datasets cannot be empty.", "Datasets cannot be empty.");
        }

        try
        {
            var pngBytes = await _imageEngineClient.RenderChartAsync(request, ct);

            var titleOrType = string.IsNullOrWhiteSpace(request.Title)
                ? request.Type.ToLowerInvariant()
                : request.Title.Trim();

            var slug = SlugRegex.Replace(titleOrType.ToLowerInvariant().Replace(' ', '-'), "");
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = request.Type.ToLowerInvariant();
            }

            var fileName = $"chart-{slug}-{Guid.NewGuid().ToString("N")[..8]}.png";
            var linkText = string.IsNullOrWhiteSpace(request.Title)
                ? $"{request.Type} chart"
                : request.Title.Trim();

            var altText = string.IsNullOrWhiteSpace(request.Subtitle)
                ? $"Chart: {linkText}"
                : $"Chart: {linkText} - {request.Subtitle.Trim()}";

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
            var message = $"Successfully rendered {request.Type} chart and attached to draft: {tag}";

            return new ChartResult(true, uploadResult.Id, tag, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render and store chart for draft {DraftId}", draftId);
            return new ChartResult(false, null, null, $"Failed to render chart: {ex.Message}", ex.Message);
        }
    }
}
