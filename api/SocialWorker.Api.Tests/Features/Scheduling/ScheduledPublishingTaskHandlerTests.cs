using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Scheduling.Handlers;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class ScheduledPublishingTaskHandlerTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly StubPublisher _publisher;
    private readonly StubPublisherResolver _resolver;
    private readonly PublishValidationService _validationService;
    private readonly SocialWorker.Api.Features.Publishing.Resilience.PublishOutcomeRecorder _outcomeRecorder;
    private readonly ScheduledPublishingTaskHandler _handler;

    public ScheduledPublishingTaskHandlerTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
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
        _outcomeRecorder = new SocialWorker.Api.Features.Publishing.Resilience.PublishOutcomeRecorder(_db);
        _handler = new ScheduledPublishingTaskHandler(
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            NullLogger<ScheduledPublishingTaskHandler>.Instance);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    private async Task<(Schedule Schedule, Draft Draft, PlatformThread Thread, Account Account, ScheduleQueueItem QueueItem)> CreateTestDataAsync()
    {
        var schedule = Schedule.Create(_user.Id, "Primary Queue");
        _db.Schedules.Add(schedule);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Platform = "Bluesky",
            Handle = "test.bsky.social",
            CredentialsEncrypted = "token"
        };
        _db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Scheduled Draft",
            Content = "Hello from scheduled publishing!"
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            AccountId = account.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Scheduled,
            Content = "Hello from scheduled publishing!"
        };
        _db.PlatformThreads.Add(thread);

        var item = ScheduleQueueItem.Create(schedule.Id, draft.Id, thread.Id, 0);
        item.Status = ScheduleQueueItemStatus.Dispatched;
        item.AssignedSlotUtc = DateTime.UtcNow.AddMinutes(15);
        _db.ScheduleQueueItems.Add(item);

        await _db.SaveChangesAsync();
        return (schedule, draft, thread, account, item);
    }

    [Fact]
    public async Task ExecuteAsync_Success_CreatesPostSetsSentAndSucceeded()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();

        _publisher.Result = new PublishResult
        {
            Success = true,
            Posts = new List<PublishedPost>
            {
                new() { SegmentIndex = 0, RemoteId = "at://did:plc:123/app.bsky.feed.post/456", Url = "https://bsky.app/profile/test/post/456" }
            }
        };

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext { AttemptCount = 1, MaxAttempts = 3 };

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);

        var updatedItem = await _db.ScheduleQueueItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal(ScheduleQueueItemStatus.Succeeded, updatedItem.Status);
        Assert.Null(updatedItem.LastError);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(posts);
        Assert.Equal("https://bsky.app/profile/test/post/456", posts[0].Url);
    }

    [Fact]
    public async Task ExecuteAsync_TransientError_ReturnsRetryAndStoresError()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();

        _publisher.Result = new PublishResult
        {
            Success = false,
            ErrorMessage = "Rate limit reached (429)"
        };

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext { AttemptCount = 1, MaxAttempts = 3 };

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Retry, result.Outcome);
        Assert.Equal("Rate limit reached (429)", result.ErrorMessage);

        var updatedItem = await _db.ScheduleQueueItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal("Rate limit reached (429)", updatedItem.LastError);
    }

    [Fact]
    public async Task ExecuteAsync_TerminalError_ExceedsMaxAttempts_SetsFailed()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();

        _publisher.Result = new PublishResult
        {
            Success = false,
            ErrorMessage = "Token revoked"
        };

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext { AttemptCount = 3, MaxAttempts = 3 };

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Fatal, result.Outcome);

        var updatedItem = await _db.ScheduleQueueItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal(ScheduleQueueItemStatus.Failed, updatedItem.Status);
        Assert.Equal("Token revoked", updatedItem.LastError);
    }

    [Fact]
    public async Task ExecuteAsync_PausedQueueItem_SkipsExecution()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();
        item.Status = ScheduleQueueItemStatus.Paused;
        await _db.SaveChangesAsync();

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        Assert.False(_publisher.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadySucceeded_SkipsExecution()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();
        item.Status = ScheduleQueueItemStatus.Succeeded;
        await _db.SaveChangesAsync();

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        Assert.False(_publisher.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_ThreadAlreadySent_MarksQueueItemSucceededAndDoesNotPublish()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();
        thread.Stage = PlatformThreadStage.Sent;
        await _db.SaveChangesAsync();

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        Assert.False(_publisher.WasCalled);

        var updatedItem = await _db.ScheduleQueueItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal(ScheduleQueueItemStatus.Succeeded, updatedItem.Status);
        Assert.Null(updatedItem.LastError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_InvokesAgentRepairAndPublishesIfRepairSucceeds()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();
        thread.Content = new string('x', 301);
        await _db.SaveChangesAsync();

        var fakeRepairService = new StubDraftAutoRepairService
        {
            RepairHandler = t =>
            {
                t.Content = "Repaired and valid short post";
                return Task.FromResult(SocialWorker.Api.Features.Drafts.Services.DraftRepairResult.Succeeded(t.Content));
            }
        };

        var handlerWithRepair = new ScheduledPublishingTaskHandler(
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            NullLogger<ScheduledPublishingTaskHandler>.Instance,
            fakeRepairService);

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext();

        var result = await handlerWithRepair.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        Assert.True(_publisher.WasCalled);
        Assert.True(fakeRepairService.WasCalled);
        Assert.Equal("Repaired and valid short post", thread.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFailsAndRepairFails_MarksQueueItemFailed()
    {
        var (_, draft, thread, _, item) = await CreateTestDataAsync();
        thread.Content = new string('x', 301);
        await _db.SaveChangesAsync();

        var fakeRepairService = new StubDraftAutoRepairService
        {
            RepairHandler = t => Task.FromResult(SocialWorker.Api.Features.Drafts.Services.DraftRepairResult.Failed("Could not shorten"))
        };

        var handlerWithRepair = new ScheduledPublishingTaskHandler(
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            NullLogger<ScheduledPublishingTaskHandler>.Instance,
            fakeRepairService);

        var payload = new ScheduledPublishingPayload(item.Id, item.ScheduleId, draft.Id, thread.Id);
        var context = new TestTaskExecutionContext();

        var result = await handlerWithRepair.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Fatal, result.Outcome);
        Assert.False(_publisher.WasCalled);
        Assert.True(fakeRepairService.WasCalled);

        var updatedItem = await _db.ScheduleQueueItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal(ScheduleQueueItemStatus.Failed, updatedItem.Status);
    }

    private sealed class StubDraftAutoRepairService : SocialWorker.Api.Features.Drafts.Services.IDraftAutoRepairService
    {
        public bool WasCalled { get; private set; }
        public Func<PlatformThread, Task<SocialWorker.Api.Features.Drafts.Services.DraftRepairResult>>? RepairHandler { get; set; }

        public Task<SocialWorker.Api.Features.Drafts.Services.DraftRepairResult> RepairDraftAsync(Draft draft, string platform, string validationError, Guid userId, CancellationToken ct)
        {
            WasCalled = true;
            return Task.FromResult(SocialWorker.Api.Features.Drafts.Services.DraftRepairResult.Succeeded(draft.Content ?? ""));
        }

        public Task<SocialWorker.Api.Features.Drafts.Services.DraftRepairResult> RepairPlatformThreadAsync(PlatformThread thread, string validationError, Guid userId, CancellationToken ct)
        {
            WasCalled = true;
            if (RepairHandler != null)
            {
                return RepairHandler(thread);
            }
            return Task.FromResult(SocialWorker.Api.Features.Drafts.Services.DraftRepairResult.Succeeded(thread.Content ?? ""));
        }
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

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(Result);
        }
    }

    private sealed class TestTaskExecutionContext : ITaskExecutionContext
    {
        public Guid TaskId { get; init; } = Guid.NewGuid();
        public string TaskType { get; init; } = ScheduledPublishingTaskHandler.TaskTypeName;
        public int AttemptCount { get; init; } = 1;
        public int MaxAttempts { get; init; } = 3;
        public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

        public Task<bool> ExtendLockAsync(TimeSpan additionalTime, CancellationToken ct = default) => Task.FromResult(true);
    }
}
