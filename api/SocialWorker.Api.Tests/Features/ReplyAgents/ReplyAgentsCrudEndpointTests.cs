using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class ReplyAgentsCrudEndpointTests : SqliteTestBase
{
    [Fact]
    public async Task AssignedPostsCount_IsPopulatedOnReplyAgentDto()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var agent = ReplyAgent.Create(user.Id, "Agent A", "Persona");
        db.ReplyAgents.Add(agent);
        await db.SaveChangesAsync();

        var post1 = Post.CreateExternal(user.Id, "Bluesky", "at://did:plc:123/post/1", null, "Hello 1", "https://bsky.app/post/1", "@author");
        var post2 = Post.CreateExternal(user.Id, "Bluesky", "at://did:plc:123/post/2", null, "Hello 2", "https://bsky.app/post/2", "@author");
        db.Posts.AddRange(post1, post2);
        await db.SaveChangesAsync();

        db.ReplyAgentPosts.Add(ReplyAgentPost.Create(agent.Id, post1.Id));
        db.ReplyAgentPosts.Add(ReplyAgentPost.Create(agent.Id, post2.Id));
        await db.SaveChangesAsync();

        var loaded = await db.ReplyAgents
            .Include(a => a.Workflows)
            .Include(a => a.AssignedPosts)
            .FirstOrDefaultAsync(a => a.Id == agent.Id);

        Assert.NotNull(loaded);
        var dto = new ReplyAgentDto(loaded);
        Assert.Equal(2, dto.AssignedPostsCount);
    }

    [Fact]
    public async Task PostService_GetAssignedPosts_CalculatesCommentAndReplyCounts()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var agent = ReplyAgent.Create(user.Id, "Agent With Metrics", "Persona");
        db.ReplyAgents.Add(agent);

        var post = Post.CreateExternal(user.Id, "Bluesky", "at://did:plc:123/post/3", null, "Viral post", "https://bsky.app/post/3", "@author");
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        db.ReplyAgentPosts.Add(ReplyAgentPost.Create(agent.Id, post.Id));

        var audit1 = new WorkflowCommentAudit
        {
            Id = Guid.NewGuid(),
            ReplyAgentId = agent.Id,
            PostId = post.Id,
            Platform = "Bluesky",
            RemoteCommentId = "comment-1",
            RemoteAuthorHandle = "@user1",
            RemoteAuthorId = "did:plc:user1",
            CommentText = "Nice post",
            CommentCreatedAt = DateTime.UtcNow,
            RankingScore = 8,
            RankingCategory = "Engagement",
            RankingDecision = "Replied",
            CreatedAt = DateTime.UtcNow
        };
        var audit2 = new WorkflowCommentAudit
        {
            Id = Guid.NewGuid(),
            ReplyAgentId = agent.Id,
            PostId = post.Id,
            Platform = "Bluesky",
            RemoteCommentId = "comment-2",
            RemoteAuthorHandle = "@user2",
            RemoteAuthorId = "did:plc:user2",
            CommentText = "Spam comment",
            CommentCreatedAt = DateTime.UtcNow,
            RankingScore = 2,
            RankingCategory = "Spam",
            RankingDecision = "Ignored",
            CreatedAt = DateTime.UtcNow
        };
        db.WorkflowCommentAudits.AddRange(audit1, audit2);
        await db.SaveChangesAsync();

        var queue = new TestBackgroundTaskQueue();
        var service = new ReplyAgentPostService(db, new StubExternalPostResolver(post), queue);

        var list = await service.GetAssignedPostsAsync(user.Id, agent.Id);
        Assert.Single(list);
        Assert.Equal(post.Id, list[0].PostId);
        Assert.Equal(2, list[0].CommentsAuditedCount);
        Assert.Equal(1, list[0].RepliesSentCount);
        Assert.True(list[0].IsActive);
    }

    private sealed class StubExternalPostResolver : IExternalPostResolver
    {
        private readonly Post _post;
        public StubExternalPostResolver(Post post) => _post = post;

        public Task<PostResolutionResult> ResolveOrCreatePostAsync(Guid userId, string postUrlOrRemoteId, string? platform = null, CancellationToken ct = default)
        {
            return Task.FromResult(PostResolutionResult.Ok(_post));
        }
    }
}
