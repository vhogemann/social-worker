using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Media;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ListDraftsToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_FiltersByWorkflowIdAndPublishedStatus()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);

        var wf1 = AutomationWorkflow.Create(user.Id, "Tech Workflow", "Instruction");
        var wf2 = AutomationWorkflow.Create(user.Id, "News Workflow", "Instruction");
        db.AutomationWorkflows.AddRange(wf1, wf2);

        var draft1 = Draft.Create(user.Id, "Published Post 1", "Content 1", SocialPlatform.Bluesky, automationWorkflowId: wf1.Id);
        var draft2 = Draft.Create(user.Id, "Draft Post 2", "Content 2", SocialPlatform.Bluesky, automationWorkflowId: wf1.Id);
        var draft3 = Draft.Create(user.Id, "Published Post 3", "Content 3", SocialPlatform.Bluesky, automationWorkflowId: wf2.Id);
        db.Drafts.AddRange(draft1, draft2, draft3);

        var thread1 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft1.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Sent, Content = "Content 1" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft, Content = "Content 2" };
        var thread3 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft3.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Sent, Content = "Content 3" };
        db.PlatformThreads.AddRange(thread1, thread2, thread3);

        var post1 = new Post { Id = Guid.NewGuid(), DraftId = draft1.Id, PlatformThreadId = thread1.Id, SegmentIndex = 0, Platform = "Bluesky", Url = "https://bsky.app/post/1" };
        var post3 = new Post { Id = Guid.NewGuid(), DraftId = draft3.Id, PlatformThreadId = thread3.Id, SegmentIndex = 0, Platform = "Bluesky", Url = "https://bsky.app/post/3" };
        db.Posts.AddRange(post1, post3);

        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider();
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);

        var tool = new ListDraftsTool(draftsService);

        // Query published drafts from wf1
        var args = new ListDraftsArgs(WorkflowId: wf1.Id, IsPublished: true);
        var result = await tool.ExecuteAsync(args, ToolExecutionContext.Create(user.Id, draft1.Id));

        Assert.Single(result.Items);
        Assert.Equal(draft1.Id, result.Items[0].Id);
        Assert.True(result.Items[0].IsPublished);
        Assert.Contains("https://bsky.app/post/1", result.Items[0].PublishedUrls);
    }
}
