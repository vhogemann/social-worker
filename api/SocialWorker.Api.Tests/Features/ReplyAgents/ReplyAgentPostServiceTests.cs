using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public class ReplyAgentPostServiceTests : SqliteTestBase
{
    private sealed class FakeExternalPostResolver : IExternalPostResolver
    {
        private readonly Func<string, PostResolutionResult> _handler;

        public FakeExternalPostResolver(Func<string, PostResolutionResult> handler)
        {
            _handler = handler;
        }

        public Task<PostResolutionResult> ResolveOrCreatePostAsync(
            Guid userId,
            string postUrlOrId,
            string? platformHint = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(_handler(postUrlOrId));
        }
    }

    [Fact]
    public async Task AssignPostAsync_WithUrl_ResolvesAndAssignsPost()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var agent = ReplyAgent.Create(user.Id, "Viral Monitor", "Polite response");
        db.ReplyAgents.Add(agent);
        await db.SaveChangesAsync();

        var externalPost = Post.CreateExternal(
            user.Id,
            "Bluesky",
            "at://did:plc:alice/app.bsky.feed.post/123",
            "cid123",
            "Hot take on tech",
            "https://bsky.app/profile/alice.bsky.social/post/123",
            "alice.bsky.social");
        db.Posts.Add(externalPost);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeExternalPostResolver(_ => PostResolutionResult.Ok(externalPost));
        var fakeQueue = new TestBackgroundTaskQueue();
        var service = new ReplyAgentPostService(db, fakeResolver, fakeQueue);

        var result = await service.AssignPostAsync(user.Id, agent.Id, new AssignReplyAgentPostRequest(
            PostUrl: "https://bsky.app/profile/alice.bsky.social/post/123"));

        Assert.True(result.Success);
        Assert.NotNull(result.AssignedPost);
        Assert.Equal(externalPost.Id, result.AssignedPost.PostId);
        Assert.Equal(agent.Id, result.AssignedPost.ReplyAgentId);

        var persisted = await db.ReplyAgentPosts.FirstOrDefaultAsync(p => p.ReplyAgentId == agent.Id && p.PostId == externalPost.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task UnassignPostAsync_RemovesAssignment()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var agent = ReplyAgent.Create(user.Id, "Viral Monitor", "Polite response");
        db.ReplyAgents.Add(agent);
        var post = Post.CreateExternal(user.Id, "Bluesky", "at://1", null, "Hello", "https://bsky.app/1", "alice");
        db.Posts.Add(post);
        var assignment = ReplyAgentPost.Create(agent.Id, post.Id);
        db.ReplyAgentPosts.Add(assignment);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeExternalPostResolver(_ => PostResolutionResult.Fail("Unused"));
        var fakeQueue = new TestBackgroundTaskQueue();
        var service = new ReplyAgentPostService(db, fakeResolver, fakeQueue);

        var unassigned = await service.UnassignPostAsync(user.Id, agent.Id, post.Id);

        Assert.True(unassigned);
        var count = await db.ReplyAgentPosts.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PollAssignedPostAsync_EnqueuesTask()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var agent = ReplyAgent.Create(user.Id, "Viral Monitor", "Polite response");
        db.ReplyAgents.Add(agent);
        var post = Post.CreateExternal(user.Id, "Bluesky", "at://1", null, "Hello", "https://bsky.app/1", "alice");
        db.Posts.Add(post);
        var assignment = ReplyAgentPost.Create(agent.Id, post.Id);
        db.ReplyAgentPosts.Add(assignment);
        await db.SaveChangesAsync();

        var fakeResolver = new FakeExternalPostResolver(_ => PostResolutionResult.Fail("Unused"));
        var fakeQueue = new TestBackgroundTaskQueue();
        var service = new ReplyAgentPostService(db, fakeResolver, fakeQueue);

        var polled = await service.PollAssignedPostAsync(user.Id, agent.Id, post.Id);

        Assert.True(polled);
        Assert.Single(fakeQueue.EnqueuedTasks);
        Assert.Equal("workflow-reply-check-post", fakeQueue.EnqueuedTasks[0].TaskType);
    }
}
