using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Scheduling;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class OutboxEndpointTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly TestBackgroundTaskQueue _taskQueue;
    private readonly ScheduleQueueService _queueService;
    private readonly ScheduleSlotEvaluatorService _evaluator;

    public OutboxEndpointTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
        _taskQueue = new TestBackgroundTaskQueue();
        _queueService = new ScheduleQueueService(_db, _taskQueue);
        _evaluator = new ScheduleSlotEvaluatorService(_db, _taskQueue);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    private async Task<(AutomationWorkflow Workflow, Draft Draft, PlatformThread Thread)> CreateWorkflowDraftAsync(PlatformThreadStage stage = PlatformThreadStage.Draft)
    {
        var workflow = AutomationWorkflow.Create(_user.Id, "Tech News Workflow", "Summarize news");
        _db.AutomationWorkflows.Add(workflow);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            AutomationWorkflowId = workflow.Id,
            Title = "AI News Roundup",
            Content = "Important AI developments today"
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = stage,
            Content = "Important AI developments today"
        };
        _db.PlatformThreads.Add(thread);

        await _db.SaveChangesAsync();
        return (workflow, draft, thread);
    }

    [Fact]
    public async Task Outbox_OnlyReturnsPendingWorkflowDrafts()
    {
        var (wf1, pendingDraft, _) = await CreateWorkflowDraftAsync(PlatformThreadStage.Draft);
        var (wf2, sentDraft, _) = await CreateWorkflowDraftAsync(PlatformThreadStage.Sent);

        var manualDraft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Manual Draft Without Workflow",
            Content = "Manual content"
        };
        _db.Drafts.Add(manualDraft);
        await _db.SaveChangesAsync();

        var outboxDrafts = await _db.Drafts
            .Where(d => d.UserId == _user.Id &&
                        d.AutomationWorkflowId != null &&
                        d.Status != DraftStatus.Archived &&
                        d.Status != DraftStatus.Deleted)
            .Include(d => d.Threads)
            .Where(d => d.Threads.Any(t => t.Stage == PlatformThreadStage.Draft))
            .ToListAsync();

        Assert.Single(outboxDrafts);
        Assert.Equal(pendingDraft.Id, outboxDrafts[0].Id);
    }

    [Fact]
    public async Task ApproveOutbox_QueueAction_EnqueuesIntoSchedule()
    {
        var (workflow, draft, thread) = await CreateWorkflowDraftAsync();
        var schedule = Schedule.Create(_user.Id, "Weekly News");
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        var item = await _queueService.EnqueueAsync(schedule.Id, thread.Id, draft.Id);

        Assert.NotNull(item);
        Assert.Equal(schedule.Id, item.ScheduleId);
        Assert.Equal(ScheduleQueueItemStatus.Queued, item.Status);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Scheduled, updatedThread.Stage);
    }

    [Fact]
    public async Task DismissOutbox_ArchivesDraft()
    {
        var (workflow, draft, _) = await CreateWorkflowDraftAsync();

        draft.Status = DraftStatus.Archived;
        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var archived = await _db.Drafts.FindAsync(draft.Id);
        Assert.NotNull(archived);
        Assert.Equal(DraftStatus.Archived, archived.Status);
    }
}
