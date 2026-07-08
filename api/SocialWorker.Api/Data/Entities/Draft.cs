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
    public ICollection<ThreadSegment> Segments { get; set; } = new List<ThreadSegment>();
    public ICollection<PlatformThread> Threads { get; set; } = new List<PlatformThread>();
    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}