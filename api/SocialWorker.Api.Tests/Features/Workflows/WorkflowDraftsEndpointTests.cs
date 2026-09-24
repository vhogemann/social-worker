using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Workflows;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class WorkflowDraftsEndpointTests : SqliteTestBase
{
    [Fact]
    public async Task GetWorkflowDrafts_ReturnsOnlyCanonicalDraftsForSpecifiedWorkflow()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var workflow1 = AutomationWorkflow.Create(user.Id, "Workflow 1", "Prompt 1");
        var workflow2 = AutomationWorkflow.Create(user.Id, "Workflow 2", "Prompt 2");
        db.AutomationWorkflows.AddRange(workflow1, workflow2);
        await db.SaveChangesAsync();

        var wf1Draft1 = Draft.Create(user.Id, "WF1 Draft 1", "Content 1", null, workflow1.Id);
        var wf1Draft2 = Draft.Create(user.Id, "WF1 Draft 2", "Content 2", null, workflow1.Id);
        var wf2Draft = Draft.Create(user.Id, "WF2 Draft", "Content 3", null, workflow2.Id);
        var adHocDraft = Draft.Create(user.Id, "Ad-Hoc Draft", "Content 4");
        var deletedDraft = Draft.Create(user.Id, "Deleted Draft", "Content 5", null, workflow1.Id);
        deletedDraft.Status = DraftStatus.Deleted;

        db.Drafts.AddRange(wf1Draft1, wf1Draft2, wf2Draft, adHocDraft, deletedDraft);
        await db.SaveChangesAsync();

        var variant = Draft.Create(user.Id, "Variant of Draft 1", "Variant Content", SocialPlatform.Bluesky, workflow1.Id);
        variant.CanonicalDraftId = wf1Draft1.Id;
        db.Drafts.Add(variant);
        await db.SaveChangesAsync();

        var drafts = await db.Drafts
            .Include(d => d.Threads).ThenInclude(t => t.Account)
            .Where(d => d.AutomationWorkflowId == workflow1.Id
                      && d.CanonicalDraftId == null
                      && d.Status != DraftStatus.Deleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, drafts.Count);
        var dtos = drafts.Select(d => new WorkflowDraftSummaryDto(d)).ToList();

        Assert.Equal(2, dtos.Count);
        Assert.Contains(dtos, d => d.Id == wf1Draft1.Id && d.Title == "WF1 Draft 1");
        Assert.Contains(dtos, d => d.Id == wf1Draft2.Id && d.Title == "WF1 Draft 2");
        Assert.DoesNotContain(dtos, d => d.Id == wf2Draft.Id);
        Assert.DoesNotContain(dtos, d => d.Id == adHocDraft.Id);
        Assert.DoesNotContain(dtos, d => d.Id == deletedDraft.Id);
        Assert.DoesNotContain(dtos, d => d.Id == variant.Id);
    }

    [Fact]
    public async Task GetWorkflowDrafts_IncludesPlatformThreadInfo()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Bluesky",
            Handle = "test.bsky.social",
            CredentialsEncrypted = "token"
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var workflow = AutomationWorkflow.Create(user.Id, "Workflow", "Prompt");
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var draft = Draft.Create(user.Id, "Workflow Draft with Thread", "Content", null, workflow.Id);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var thread = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            AccountId = account.Id,
            Account = account,
            Stage = PlatformThreadStage.Sent,
            Content = "Post content"
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var post = new Post
        {
            PlatformThreadId = thread.Id,
            Platform = "Bluesky",
            RemoteId = "at://did:plc:123/app.bsky.feed.post/456",
            Url = "https://bsky.app/profile/test.bsky.social/post/456",
            SegmentIndex = 0
        };
        db.Posts.Add(post);
        var loaded = await db.Drafts
            .Include(d => d.Threads).ThenInclude(t => t.Account)
            .FirstAsync(d => d.Id == draft.Id);

        var dto = new WorkflowDraftSummaryDto(loaded);
        Assert.Equal("Workflow Draft with Thread", dto.Title);
        Assert.Single(dto.Threads);
        Assert.Equal("Bluesky", dto.Threads[0].Platform);
        Assert.Equal("Sent", dto.Threads[0].Stage);
        Assert.Equal("test.bsky.social", dto.Threads[0].AccountHandle);
        Assert.Null(dto.Threads[0].VariantId);
    }

    [Fact]
    public async Task GetWorkflowDrafts_IncludesPlatformVariantDraftId()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var workflow = AutomationWorkflow.Create(user.Id, "Workflow", "Prompt");
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var canonicalDraft = Draft.Create(user.Id, "Canonical Draft", "Content", null, workflow.Id);
        db.Drafts.Add(canonicalDraft);
        await db.SaveChangesAsync();

        var variantDraft = Draft.Create(user.Id, "Canonical Draft (Bluesky)", "Variant Content", SocialPlatform.Bluesky, workflow.Id);
        variantDraft.CanonicalDraftId = canonicalDraft.Id;
        db.Drafts.Add(variantDraft);
        await db.SaveChangesAsync();

        var variantThread = new PlatformThread
        {
            DraftId = variantDraft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Sent,
            Content = "Bluesky thread content"
        };
        db.PlatformThreads.Add(variantThread);
        await db.SaveChangesAsync();

        var loaded = await db.Drafts
            .Include(d => d.Threads).ThenInclude(t => t.Account)
            .Include(d => d.Variants).ThenInclude(v => v.Threads).ThenInclude(t => t.Account)
            .FirstAsync(d => d.Id == canonicalDraft.Id);

        var dto = new WorkflowDraftSummaryDto(loaded);
        Assert.Single(dto.Threads);
        Assert.Equal("Bluesky", dto.Threads[0].Platform);
        Assert.Equal("Sent", dto.Threads[0].Stage);
        Assert.Equal(variantDraft.Id, dto.Threads[0].VariantId);
    }

    [Fact]
    public async Task RetryFailedDrafts_FindsAndUpdatesFailedDrafts()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var workflow = AutomationWorkflow.Create(user.Id, "Workflow", "Prompt");
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var failedDraft1 = Draft.Create(user.Id, "Failed Draft 1", "Content", null, workflow.Id);
        failedDraft1.Status = DraftStatus.Failed;
        var failedDraft2 = Draft.Create(user.Id, "Failed Draft 2", "Content", null, workflow.Id);
        failedDraft2.Status = DraftStatus.Failed;
        var successfulDraft = Draft.Create(user.Id, "Successful Draft", "Content", null, workflow.Id);
        successfulDraft.Status = DraftStatus.Editing;

        db.Drafts.AddRange(failedDraft1, failedDraft2, successfulDraft);
        await db.SaveChangesAsync();

        var failedDrafts = await db.Drafts
            .Where(d => d.AutomationWorkflowId == workflow.Id
                     && d.UserId == user.Id
                     && d.CanonicalDraftId == null
                     && d.Status == DraftStatus.Failed)
            .ToListAsync();

        Assert.Equal(2, failedDrafts.Count);

        foreach (var d in failedDrafts)
        {
            d.Status = DraftStatus.Formatting;
        }
        await db.SaveChangesAsync();

        var reloaded = await db.Drafts
            .Where(d => d.AutomationWorkflowId == workflow.Id && d.CanonicalDraftId == null)
            .ToListAsync();

        Assert.All(reloaded.Where(d => d.Id == failedDraft1.Id || d.Id == failedDraft2.Id), d => Assert.Equal(DraftStatus.Formatting, d.Status));
        Assert.Equal(DraftStatus.Editing, reloaded.First(d => d.Id == successfulDraft.Id).Status);
    }
}

