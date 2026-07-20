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
}
