using System;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class Source
{
    public Guid Id { get; set; }
    public SourceKind Kind { get; set; }
    public string Reference { get; set; } = "";
    public string? Content { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public SourceProcessingStatus ProcessingStatus { get; set; } = SourceProcessingStatus.Pending;
    public string? TranscriptPath { get; set; }
    public string? YoutubeVideoId { get; set; }
    public string? Sha256 { get; set; }
    public ICollection<DraftSource> DraftSources { get; set; } = new List<DraftSource>();
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public static Source CreateUrl(string url, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL reference cannot be empty.", nameof(url));

        return new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.Url,
            Reference = url.Trim(),
            Title = title?.Trim(),
            ProcessingStatus = SourceProcessingStatus.Pending,
            AddedAt = DateTime.UtcNow
        };
    }

    public static Source CreateFile(string filePath, string? title = null, string? sha256 = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path reference cannot be empty.", nameof(filePath));

        return new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.File,
            Reference = filePath.Trim(),
            Title = title?.Trim(),
            Sha256 = sha256,
            ProcessingStatus = SourceProcessingStatus.Pending,
            AddedAt = DateTime.UtcNow
        };
    }

    public static Source CreateYouTube(string videoId, string referenceUrl, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            throw new ArgumentException("YouTube video ID cannot be empty.", nameof(videoId));
        if (string.IsNullOrWhiteSpace(referenceUrl))
            throw new ArgumentException("Reference URL cannot be empty.", nameof(referenceUrl));

        return new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.YouTube,
            YoutubeVideoId = videoId.Trim(),
            Reference = referenceUrl.Trim(),
            Title = title?.Trim(),
            ProcessingStatus = SourceProcessingStatus.Pending,
            AddedAt = DateTime.UtcNow
        };
    }
}
