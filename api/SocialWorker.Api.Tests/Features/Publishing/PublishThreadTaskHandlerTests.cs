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
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Handlers;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Models;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing;

public sealed class PublishThreadTaskHandlerTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly StubPublisher _publisher;
    private readonly StubPublisherResolver _resolver;
    private readonly PublishValidationService _validationService;
    private readonly PublishOutcomeRecorder _outcomeRecorder;
    private readonly PublishThreadTaskHandler _handler;

    public PublishThreadTaskHandlerTests()
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
        _outcomeRecorder = new PublishOutcomeRecorder(_db);
        _handler = new PublishThreadTaskHandler(
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            NullLogger<PublishThreadTaskHandler>.Instance);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    private async Task<(Draft Draft, PlatformThread Thread, Account Account)> CreateTestDataAsync(string platform = "Bluesky", string content = "Hello Bluesky!")
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Platform = platform,
            Handle = "test.handle",
            CredentialsEncrypted = "token"
        };
        _db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Publish Draft",
            Content = content
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            AccountId = account.Id,
            Platform = platform,
            Stage = PlatformThreadStage.Scheduled,
            Content = content
        };
        _db.PlatformThreads.Add(thread);

        await _db.SaveChangesAsync();
        return (draft, thread, account);
    }

    [Fact]
    public async Task ExecuteAsync_ValidThread_PublishesAndRecordsOutcome_ReturnsSuccess()
    {
        var (draft, thread, account) = await CreateTestDataAsync();
        _publisher.NextResult = PublishResult.Succeeded(new List<PublishedPost>
        {
            new() { SegmentIndex = 0, RemoteId = "post-1", Url = "https://bsky.app/post/1" }
        });

        var payload = new PublishThreadTaskPayload(draft.Id, thread.Id, _user.Id, "Bluesky", account.Id);
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        var reloadedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(reloadedThread);
        Assert.Equal(PlatformThreadStage.Sent, reloadedThread.Stage);
        Assert.Null(reloadedThread.LastPublishError);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(posts);
        Assert.Equal("post-1", posts[0].RemoteId);
    }

    [Fact]
    public async Task ExecuteAsync_ThreadNotFound_ReturnsFatal()
    {
        var payload = new PublishThreadTaskPayload(Guid.NewGuid(), Guid.NewGuid(), _user.Id, "Bluesky");
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Fatal, result.Outcome);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ThreadAlreadySent_ReturnsSuccess()
    {
        var (draft, thread, account) = await CreateTestDataAsync();
        thread.Stage = PlatformThreadStage.Sent;
        await _db.SaveChangesAsync();

        var payload = new PublishThreadTaskPayload(draft.Id, thread.Id, _user.Id, "Bluesky", account.Id);
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        Assert.False(_publisher.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_NoConnectedAccount_ReturnsFatal()
    {
        var draft = new Draft { Id = Guid.NewGuid(), UserId = _user.Id, Title = "Draft", Content = "Text" };
        _db.Drafts.Add(draft);
        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "Twitter", Content = "Text" };
        _db.PlatformThreads.Add(thread);
        await _db.SaveChangesAsync();

        var payload = new PublishThreadTaskPayload(draft.Id, thread.Id, _user.Id, "Twitter");
        var context = new TestTaskExecutionContext();

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Fatal, result.Outcome);
        Assert.Contains("No connected account found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PublisherFails_ReturnsRetry()
    {
        var (draft, thread, account) = await CreateTestDataAsync();
        _publisher.NextResult = PublishResult.Failed("API rate limit exceeded.");

        var payload = new PublishThreadTaskPayload(draft.Id, thread.Id, _user.Id, "Bluesky", account.Id);
        var context = new TestTaskExecutionContext { AttemptCount = 1, MaxAttempts = 3 };

        var result = await _handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Retry, result.Outcome);
        Assert.Contains("rate limit", result.ErrorMessage);

        var reloadedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(reloadedThread);
        Assert.Equal("API rate limit exceeded.", reloadedThread.LastPublishError);
    }

    private sealed class StubPublisher : IPublisher
    {
        public string Platform => "Bluesky";
        public bool WasCalled { get; private set; }
        public PublishResult NextResult { get; set; } = PublishResult.Succeeded(new List<PublishedPost>());

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class StubPublisherResolver : IPublisherResolver
    {
        private readonly IPublisher _publisher;
        public StubPublisherResolver(IPublisher publisher) => _publisher = publisher;
        public IPublisher? Resolve(string platform) => _publisher;
    }

    private sealed class TestTaskExecutionContext : ITaskExecutionContext
    {
        public Guid TaskId { get; init; } = Guid.NewGuid();
        public string TaskType { get; init; } = PublishThreadTaskHandler.TaskTypeName;
        public int AttemptCount { get; set; } = 1;
        public int MaxAttempts { get; set; } = 3;
        public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

        public Task<bool> ExtendLockAsync(TimeSpan additionalTime, CancellationToken ct = default) => Task.FromResult(true);
    }
}
