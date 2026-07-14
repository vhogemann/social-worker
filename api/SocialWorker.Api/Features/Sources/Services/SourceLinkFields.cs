using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Sources;

public sealed record SourceLinkFieldsResult(
    string CanonicalUrl,
    string CitationLabel,
    string EmbedKind,
    string? CanonicalEmbedMarkdown,
    string PlainLinkLine);

public static class SourceLinkFields
{
    public static SourceLinkFieldsResult Build(Guid sourceId, SourceKind kind, string reference, string? title)
    {
        var safeReference = reference ?? string.Empty;
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

        return kind switch
        {
            SourceKind.File => BuildFile(sourceId, safeReference, normalizedTitle),
            SourceKind.YouTube => BuildYouTube(safeReference, normalizedTitle),
            _ => BuildUrl(safeReference, normalizedTitle)
        };
    }

    private static SourceLinkFieldsResult BuildUrl(string reference, string? title)
    {
        var canonicalUrl = reference;
        var citationLabel = !string.IsNullOrWhiteSpace(title) ? title : canonicalUrl;
        var embed = $"[{citationLabel}]({canonicalUrl})";
        return new SourceLinkFieldsResult(canonicalUrl, citationLabel, "url", embed, canonicalUrl);
    }

    private static SourceLinkFieldsResult BuildYouTube(string reference, string? title)
    {
        var canonicalUrl = reference;
        var citationLabel = !string.IsNullOrWhiteSpace(title) ? title : "YouTube Video";
        var embed = $"![{citationLabel}]({canonicalUrl})";
        return new SourceLinkFieldsResult(canonicalUrl, citationLabel, "youtube", embed, canonicalUrl);
    }

    private static SourceLinkFieldsResult BuildFile(Guid sourceId, string reference, string? title)
    {
        var canonicalUrl = $"file://{sourceId}";
        var displayName = !string.IsNullOrWhiteSpace(title) ? title : reference;
        var citationLabel = $"File: {displayName}";
        var embed = $"[{citationLabel}]({canonicalUrl})";
        return new SourceLinkFieldsResult(canonicalUrl, citationLabel, "file", embed, canonicalUrl);
    }
}
