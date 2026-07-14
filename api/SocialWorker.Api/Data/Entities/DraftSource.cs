namespace SocialWorker.Api.Data.Entities;

public class DraftSource
{
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}