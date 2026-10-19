using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing;

public sealed class PublishingEndpointTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly ClaimsPrincipal _principal;
    private readonly StubPublisher _publisher;
    private readonly StubPublisherResolver _resolver;
    private readonly PublishValidationService _validationService;
    private readonly PublishOutcomeRecorder _outcomeRecorder;
    private readonly IPublishLockService _lockService;

    public PublishingEndpointTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
        _principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString()) }, "Test"));
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
        _lockService = new PublishLockService();
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    private async Task<(Draft Draft, PlatformThread Thread, Account Account)> CreateTestDataAsync(PlatformThreadStage stage = PlatformThreadStage.Draft)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Platform = "Bluesky",
            Handle = "tester.bsky.social",
            CredentialsEncrypted = "token"
        };
        _db.Accounts.Add(account);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Title = "Test Draft",
            Content = "Test content"
        };
        _db.Drafts.Add(draft);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            AccountId = account.Id,
            Platform = "Bluesky",
            Stage = stage,
            Content = "Hello from Bluesky thread"
        };
        _db.PlatformThreads.Add(thread);

        await _db.SaveChangesAsync();
        return (draft, thread, account);
    }

    [Fact]
    public async Task PublishThread_FirstTime_PublishesAndRecordsOutcome()
    {
        var (draft, thread, _) = await CreateTestDataAsync(PlatformThreadStage.Draft);

        _publisher.Result = new PublishResult
        {
            Success = true,
            Posts = new List<PublishedPost>
            {
                new() { SegmentIndex = 0, RemoteId = "at://123", Url = "https://bsky.app/post/123" }
            }
        };

        var result = await PublishingEndpoint.PublishThreadAsync(
            _principal,
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            _lockService,
            draft.Id,
            thread.Id,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<PublishResult>>(result);
        Assert.NotNull(okResult.Value);
        Assert.True(okResult.Value.Success);
        Assert.True(_publisher.WasCalled);

        var updatedThread = await _db.PlatformThreads.FindAsync(thread.Id);
        Assert.NotNull(updatedThread);
        Assert.Equal(PlatformThreadStage.Sent, updatedThread.Stage);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync();
        Assert.Single(posts);
        Assert.Equal("https://bsky.app/post/123", posts[0].Url);
    }

    [Fact]
    public async Task PublishThread_AlreadySent_ReturnsExistingPostsIdempotentlyWithoutCallingPublisher()
    {
        var (draft, thread, _) = await CreateTestDataAsync(PlatformThreadStage.Sent);

        var existingPost = new Post
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            SegmentIndex = 0,
            Platform = "Bluesky",
            RemoteId = "at://existing/123",
            Url = "https://bsky.app/post/existing/123",
            AuthorHandle = "tester.bsky.social"
        };
        _db.Posts.Add(existingPost);
        await _db.SaveChangesAsync();

        var result = await PublishingEndpoint.PublishThreadAsync(
            _principal,
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            _lockService,
            draft.Id,
            thread.Id,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<PublishResult>>(result);
        Assert.NotNull(okResult.Value);
        Assert.True(okResult.Value.Success);
        Assert.Single(okResult.Value.Posts);
        Assert.Equal("https://bsky.app/post/existing/123", okResult.Value.Posts[0].Url);
        Assert.False(_publisher.WasCalled);
    }

    [Fact]
    public async Task PublishThread_WhenLocked_ReturnsConflict()
    {
        var (draft, thread, _) = await CreateTestDataAsync(PlatformThreadStage.Draft);

        // Acquire lock beforehand to simulate an in-flight operation
        using var lockHandle = await _lockService.TryAcquireLockAsync(thread.Id, TimeSpan.FromSeconds(1));
        Assert.NotNull(lockHandle);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var result = await PublishingEndpoint.PublishThreadAsync(
            _principal,
            _db,
            _resolver,
            _validationService,
            _outcomeRecorder,
            _lockService,
            draft.Id,
            thread.Id,
            cts.Token);

        var conflictResult = Assert.IsType<Conflict<ApiErrorResponse>>(result);
        Assert.Equal("A publishing operation is currently in progress for this thread.", conflictResult.Value?.Error);
        Assert.False(_publisher.WasCalled);
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
}
