using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.ReplyAgents.Services;

public sealed class CommentEnrichmentService : ICommentEnrichmentService
{
    private readonly WebScraperService _webScraper;
    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ModelCapabilityProbe _capabilityProbe;
    private readonly ILogger<CommentEnrichmentService> _logger;
    private readonly ITranscriptExtractionService? _transcriber;
    private readonly IYouTubeSourceService? _youTubeSourceService;

    public CommentEnrichmentService(
        WebScraperService webScraper,
        AppDbContext db,
        LlmProviderService providerService,
        ILlmProviderAdapter adapter,
        ModelCapabilityProbe capabilityProbe,
        ILogger<CommentEnrichmentService> logger,
        ITranscriptExtractionService? transcriber = null,
        IYouTubeSourceService? youTubeSourceService = null)
    {
        _webScraper = webScraper;
        _db = db;
        _providerService = providerService;
        _adapter = adapter;
        _capabilityProbe = capabilityProbe;
        _logger = logger;
        _transcriber = transcriber;
        _youTubeSourceService = youTubeSourceService;
    }

    public async Task<EnrichedCommentContext> EnrichCommentAsync(
        PlatformCommentItem comment,
        AppUser user,
        CancellationToken ct = default)
    {
        var urls = (comment.ExtractedUrls ?? Array.Empty<string>()).ToList();
        if (urls.Count == 0 && !string.IsNullOrWhiteSpace(comment.Text))
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(comment.Text, @"https?://[^\s""'<>]+");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (Uri.TryCreate(m.Value, UriKind.Absolute, out _))
                {
                    urls.Add(m.Value);
                }
            }
        }

        var parsedLinks = await EnrichLinksAsync(urls, ct);
        var parsedImages = await EnrichImagesAsync(comment.ExtractedImages, user, ct);

        var formattedContext = BuildFormattedContext(comment, parsedLinks, parsedImages);

        return new EnrichedCommentContext(
            Comment: comment,
            Links: parsedLinks,
            Images: parsedImages,
            FormattedContextForLlm: formattedContext
        );
    }

    private async Task<IReadOnlyList<ParsedLinkContext>> EnrichLinksAsync(
        IReadOnlyList<string> urls,
        CancellationToken ct)
    {
        if (urls == null || urls.Count == 0) return Array.Empty<ParsedLinkContext>();

        var results = new List<ParsedLinkContext>();
        foreach (var url in urls.Take(3))
        {
            if (_transcriber != null && _youTubeSourceService != null && _youTubeSourceService.IsYouTubeUrl(url))
            {
                try
                {
                    var safeId = Guid.NewGuid().ToString("N");
                    var filename = $"comment_{safeId}.json";
                    var extract = await _transcriber.ExtractAsync(url, filename, ct);
                    if (extract.Success && !string.IsNullOrWhiteSpace(extract.TranscriptPath))
                    {
                        var doc = await _transcriber.ReadTranscriptAsync(extract.TranscriptPath, ct);
                        if (doc != null && !string.IsNullOrWhiteSpace(doc.Transcript))
                        {
                            var snippet = doc.Transcript.Length > 1500 ? doc.Transcript[..1500] + "..." : doc.Transcript;
                            results.Add(new ParsedLinkContext(url, "Video Transcript", snippet, true));
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to transcribe video URL in comment: {Url}", url);
                }
            }

            try
            {
                var scrape = await _webScraper.ScrapeUrlAsync(url);
                if (scrape.Success && !string.IsNullOrWhiteSpace(scrape.Content))
                {
                    var snippet = scrape.Content.Length > 800 ? scrape.Content[..800] + "..." : scrape.Content;
                    results.Add(new ParsedLinkContext(url, scrape.Title, snippet, true));
                }
                else
                {
                    results.Add(new ParsedLinkContext(url, scrape.Title, null, false));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scrape comment link: {Url}", url);
                results.Add(new ParsedLinkContext(url, null, null, false));
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<ParsedImageContext>> EnrichImagesAsync(
        IReadOnlyList<PlatformCommentImageAttachment> images,
        AppUser user,
        CancellationToken ct)
    {
        if (images == null || images.Count == 0) return Array.Empty<ParsedImageContext>();

        var results = new List<ParsedImageContext>();
        var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
        var supportsVision = false;
        LlmCredentials? credentials = null;

        if (provider != null)
        {
            var caps = await _capabilityProbe.GetCapabilitiesAsync(provider);
            supportsVision = caps.SupportsVision;
            credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);
        }

        foreach (var img in images.Take(2))
        {
            string? visualDesc = null;

            if (supportsVision && credentials != null && !string.IsNullOrWhiteSpace(img.ImageUrl))
            {
                visualDesc = await DescribeImageWithVisionAsync(img.ImageUrl, credentials, ct);
            }

            visualDesc ??= !string.IsNullOrWhiteSpace(img.AltText) ? $"[Alt text: {img.AltText}]" : null;

            results.Add(new ParsedImageContext(img.ImageUrl, img.AltText, visualDesc));
        }

        return results;
    }

    private async Task<string?> DescribeImageWithVisionAsync(
        string imageUrl,
        LlmCredentials credentials,
        CancellationToken ct)
    {
        try
        {
            var contentParts = new object[]
            {
                new { type = "text", text = "Describe what is depicted in this social media image and transcribe any visible error text, code, or charts in under 80 words." },
                new { type = "image_url", image_url = new { url = imageUrl } }
            };

            var req = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new()
                    {
                        Role = "user",
                        Content = contentParts
                    }
                },
                Stream = false
            };

            var res = await _adapter.CompleteAsync(req, credentials, ct);
            var text = res?.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
            return !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect comment image with vision LLM: {ImageUrl}", imageUrl);
            return null;
        }
    }

    private static string BuildFormattedContext(
        PlatformCommentItem comment,
        IReadOnlyList<ParsedLinkContext> links,
        IReadOnlyList<ParsedImageContext> images)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Comment by @{comment.RemoteAuthorHandle}:");
        sb.AppendLine($"\"{comment.Text}\"");

        if (images.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Attached Images:");
            for (var i = 0; i < images.Count; i++)
            {
                var img = images[i];
                var desc = img.VisualDescription ?? img.AltText ?? "No visual description available";
                sb.AppendLine($"- [Image {i + 1}]: {desc}");
            }
        }

        if (links.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Referenced Links:");
            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (string.Equals(link.Title, "Video Transcript", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"- [Video {i + 1}]: {link.Url}");
                    sb.AppendLine($"  Transcript: {link.Snippet}");
                }
                else
                {
                    var title = !string.IsNullOrWhiteSpace(link.Title) ? $"Title: \"{link.Title}\"" : "";
                    var snippet = !string.IsNullOrWhiteSpace(link.Snippet) ? $"Snippet: {link.Snippet}" : "";
                    sb.AppendLine($"- [Link {i + 1}]: {link.Url} {title} {snippet}".Trim());
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}
