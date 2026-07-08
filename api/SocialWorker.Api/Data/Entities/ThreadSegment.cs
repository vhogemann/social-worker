namespace SocialWorker.Api.Data.Entities;

public class ThreadSegment
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public int Position { get; set; }
    public string Content { get; set; } = "";
    public Draft Draft { get; set; } = null!;
}