using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Resilience;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Resilience;

public sealed class PublishOutcomeRecorderTests : SqliteTestBase
{
    [Fact]
    public async Task RecordOutcomeAsync_Success_SavesPostsAndSetsSentStage()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Test Draft", "Content");
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "Post 1\n\n---\n\nPost 2"
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var recorder = new PublishOutcomeRecorder(db);
        var posts = new List<PublishedPost>
        {
            new() { SegmentIndex = 0, RemoteId = "at://1", RemoteCid = "cid1", PublishedText = "Post 1", Url = "https://bsky.app/1" },
            new() { SegmentIndex = 1, RemoteId = "at://2", RemoteCid = "cid2", PublishedText = "Post 2", Url = "https://bsky.app/2" }
        };
        var result = PublishResult.Succeeded(posts);

        await recorder.RecordOutcomeAsync(thread, result);

        var savedPosts = await db.Posts.Where(p => p.PlatformThreadId == thread.Id).OrderBy(p => p.SegmentIndex).ToListAsync();
        Assert.Equal(2, savedPosts.Count);
        Assert.Equal("cid1", savedPosts[0].RemoteCid);
        Assert.Equal("Post 1", savedPosts[0].PublishedText);
        Assert.Equal("at://2", savedPosts[1].RemoteId);

        var updatedThread = await db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);
        Assert.Null(updatedThread.LastPublishError);
    }

    [Fact]
    public async Task RecordOutcomeAsync_Partial_SavesPostsAndSetsPartiallySentStage()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Test Draft", "Content");
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "Post 1\n\n---\n\nPost 2"
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var recorder = new PublishOutcomeRecorder(db);
        var posts = new List<PublishedPost>
        {
            new() { SegmentIndex = 0, RemoteId = "at://1", RemoteCid = "cid1", PublishedText = "Post 1", Url = "https://bsky.app/1" }
        };
        var result = PublishResult.Partial(posts, "Rate limit reached on post 2");

        await recorder.RecordOutcomeAsync(thread, result);

        var savedPosts = await db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(savedPosts);

        var updatedThread = await db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.PartiallySent, updatedThread.Stage);
        Assert.Equal("Rate limit reached on post 2", updatedThread.LastPublishError);
    }

    [Fact]
    public async Task RecordOutcomeAsync_DeduplicatesAlreadySavedPosts()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Test Draft", "Content");
        db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.PartiallySent,
            Content = "Post 1\n\n---\n\nPost 2"
        };
        db.PlatformThreads.Add(thread);

        var existingPost = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            SegmentIndex = 0,
            Platform = "Bluesky",
            RemoteId = "at://1",
            RemoteCid = "cid1",
            PublishedText = "Post 1"
        };
        db.Posts.Add(existingPost);
        await db.SaveChangesAsync();

        var recorder = new PublishOutcomeRecorder(db);
        var allPosts = new List<PublishedPost>
        {
            new() { SegmentIndex = 0, RemoteId = "at://1", RemoteCid = "cid1", PublishedText = "Post 1" },
            new() { SegmentIndex = 1, RemoteId = "at://2", RemoteCid = "cid2", PublishedText = "Post 2" }
        };
        var result = PublishResult.Succeeded(allPosts);

        await recorder.RecordOutcomeAsync(thread, result);

        var savedPosts = await db.Posts.Where(p => p.PlatformThreadId == thread.Id).OrderBy(p => p.SegmentIndex).ToListAsync();
        Assert.Equal(2, savedPosts.Count);
        Assert.Equal(existingPost.Id, savedPosts[0].Id);

        var updatedThread = await db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);
    }
}
