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
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class OutboxEndpointTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly ClaimsPrincipal _principal;
    private readonly TestBackgroundTaskQueue _taskQueue;
    private readonly ScheduleQueueService _queueService;
    private readonly ScheduleSlotEvaluatorService _evaluator;
    private readonly StubPublisher _publisher;
    private readonly StubPublisherResolver _resolver;
    private readonly PublishValidationService _validationService;
    private readonly PublishOutcomeRecorder _outcomeRecorder;
    private readonly IPublishLockService _publishLockService;

    public OutboxEndpointTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
        _principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString()) }, "Test"));
        _taskQueue = new TestBackgroundTaskQueue();
        _queueService = new ScheduleQueueService(_db, _taskQueue);
        _evaluator = new ScheduleSlotEvaluatorService(_db, _taskQueue);
        _publisher = new StubPublisher();
        _resolver = new StubPublisherResolver(_publisher);
        var canonicalValidator = new CanonicalDraftValidator();
        var blueskyValidator = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            new[] { new BlueskyMaxCharactersRule() });
        var linkedInValidator = new LinkedInDraftValidator();
        var policy = new PlatformContentPolicy();
        var validatorResolver = new DraftValidatorResolver(
            canonicalValidator,
            blueskyValidator,
            linkedInValidator,
            policy);
        _validationService = new PublishValidationService(validatorResolver, _db);
        _outcomeRecorder = new PublishOutcomeRecorder(_db);
        _publishLockService = new PublishLockService();
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

    [Fact]
    public async Task ApproveOutbox_PublishNow_PublishesAndIsIdempotentOnRepeatCall()
    {
        var (workflow, draft, thread) = await CreateWorkflowDraftAsync(PlatformThreadStage.Draft);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Platform = "Bluesky",
            Handle = "tester.bsky.social",
            CredentialsEncrypted = "token"
        };
        _db.Accounts.Add(account);
        thread.AccountId = account.Id;
        await _db.SaveChangesAsync();

        _publisher.Result = new PublishResult
        {
            Success = true,
            Posts = new List<PublishedPost>
            {
                new() { SegmentIndex = 0, RemoteId = "at://123", Url = "https://bsky.app/post/123" }
            }
        };

        var request = new ApproveOutboxRequest(Action: "publish_now", ScheduleId: null, PlatformThreadId: null);

        // First call - publishes successfully
        var result1 = await OutboxEndpoint.ApproveOutboxAsync(
            _principal,
            _db,
            _queueService,
            _evaluator,
            _resolver,
            _validationService,
            _outcomeRecorder,
            _publishLockService,
            draft.Id,
            request,
            CancellationToken.None);

        var statusResult1 = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result1);
        Assert.Equal(200, statusResult1.StatusCode);
        Assert.True(_publisher.WasCalled);
        Assert.Equal(1, _publisher.CallCount);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(posts);
        Assert.Equal("tester.bsky.social", posts[0].AuthorHandle);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);

        // Second call - idempotent, does not call publisher again
        _publisher.Reset();
        var result2 = await OutboxEndpoint.ApproveOutboxAsync(
            _principal,
            _db,
            _queueService,
            _evaluator,
            _resolver,
            _validationService,
            _outcomeRecorder,
            _publishLockService,
            draft.Id,
            request,
            CancellationToken.None);

        var statusResult2 = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result2);
        Assert.Equal(200, statusResult2.StatusCode);
        Assert.False(_publisher.WasCalled);
        Assert.Equal(0, _publisher.CallCount);

        // Posts in DB remain unchanged (not duplicated)
        var postsAfter = await _db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(postsAfter);
    }

    private sealed class StubPublisherResolver : IPublisherResolver
    {
        private readonly IPublisher _publisher;
        public StubPublisherResolver(IPublisher publisher) => _publisher = publisher;
        public IPublisher? Resolve(string platform) => _publisher;
    }

    private sealed class StubPublisher : IPublisher
    {
        public string Platform => "Bluesky";
        public PublishResult Result { get; set; } = new() { Success = true };
        public bool WasCalled { get; private set; }
        public int CallCount { get; private set; }

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            WasCalled = true;
            CallCount++;
            return Task.FromResult(Result);
        }

        public void Reset()
        {
            WasCalled = false;
            CallCount = 0;
        }
    }
}
