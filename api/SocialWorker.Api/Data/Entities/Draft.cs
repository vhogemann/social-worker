using SocialWorker.Api.Data;

namespace SocialWorker.Api.Data.Entities;

public class Draft
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "Untitled";
    public DraftStatus Status { get; set; } = DraftStatus.Editing;
    public string? Content { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public SocialPlatform? TargetPlatform { get; set; }
    public Guid? CanonicalDraftId { get; set; }
    public Draft? CanonicalDraft { get; set; }
    public ICollection<Draft> Variants { get; set; } = new List<Draft>();
    public ICollection<ThreadSegment> Segments { get; set; } = new List<ThreadSegment>();
    public ICollection<PlatformThread> Threads { get; set; } = new List<PlatformThread>();
    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public string? ChatHistory { get; set; }
    public string? ChatSummary { get; set; }
    public int LastSummarizedMessageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}