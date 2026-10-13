using System;

namespace SocialWorker.Api.Data.Entities;

public class ReplyAgentPost
{
    public Guid Id { get; set; }
    public Guid ReplyAgentId { get; set; }
    public ReplyAgent ReplyAgent { get; set; } = null!;

    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastPolledAt { get; set; }

    public static ReplyAgentPost Create(Guid replyAgentId, Guid postId)
    {
        if (replyAgentId == Guid.Empty)
            throw new ArgumentException("ReplyAgentId is required.", nameof(replyAgentId));
        if (postId == Guid.Empty)
            throw new ArgumentException("PostId is required.", nameof(postId));

        return new ReplyAgentPost
        {
            Id = Guid.NewGuid(),
            ReplyAgentId = replyAgentId,
            PostId = postId,
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        };
    }
}
