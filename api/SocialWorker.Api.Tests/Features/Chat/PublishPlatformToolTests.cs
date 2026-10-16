using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class PublishPlatformToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNoDraftId()
    {
        var tool = new PublishPlatformTool(null!, null!, null!, null!);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(Guid.NewGuid(), null));

        Assert.Contains("No active draft", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenReplyTargetRevalidationFails()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var threadId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing });
        db.PlatformThreads.Add(new PlatformThread
        {
            Id = threadId,
            DraftId = draftId,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "Hello world"
        });
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "Bluesky",
            Handle = "user.bsky.social",
            CredentialsEncrypted = "encrypted"
        });
        db.DraftBlueskyMetadata.Add(new DraftBlueskyMetadata
        {
            DraftId = draftId,
            ReplyRootUri = "at://did:plc:root/app.bsky.feed.post/1",
            ReplyRootCid = "root-cid",
            ReplyParentUri = "at://did:plc:parent/app.bsky.feed.post/2",
            ReplyParentCid = "parent-cid",
            ReplyParentUrl = "https://bsky.app/profile/example/post/2"
        });
        await db.SaveChangesAsync();

        var publisher = new StubPublisher();
        var resolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(false, "post not found"));
        var tool = BuildTool(db, publisher, resolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        Assert.False(result.Success);
        Assert.Contains("revalidated", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, publisher.CallCount);
    }

    private static PublishPlatformTool BuildTool(AppDbContext db, StubPublisher publisher, IBlueskyReplyTargetResolver resolver)
    {
        return new PublishPlatformTool(db, new StubPublisherResolver(publisher), new BlueskyContentValidator(), resolver);
    }

    private sealed class StubPublisherResolver : IPublisherResolver
    {
        private readonly IPublisher _publisher;

        public StubPublisherResolver(IPublisher publisher)
        {
            _publisher = publisher;
        }

        public IPublisher Resolve(string platform) => _publisher;
    }

    private sealed class StubPublisher : IPublisher
    {
        public string Platform => "Bluesky";
        public int CallCount { get; private set; }

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new PublishResult
            {
                Success = true,
                Posts = new List<PublishedPost>
                {
                    new() { SegmentIndex = 0, RemoteId = "remote", Url = "https://bsky.app/profile/user/post/1", PublishedText = "Live published text from platform" }
                }
            });
        }
    }

    private sealed class StubReplyTargetResolver : IBlueskyReplyTargetResolver
    {
        private readonly BlueskyReplyTargetResolutionResult _result;

        public StubReplyTargetResolver(BlueskyReplyTargetResolutionResult result)
        {
            _result = result;
        }

        public Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct)
        {
            return Task.FromResult(_result);
        }

        public Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AutoCreatesPlatformThread_WhenThreadMissingButDraftContentPresent()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing, Content = "Autonomous thread content." });
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "Bluesky",
            Handle = "user.bsky.social",
            CredentialsEncrypted = "encrypted"
        });
        await db.SaveChangesAsync();

        var stubPublisher = new StubPublisher();
        var resolver = new StubPublisherResolver(stubPublisher);
        var replyResolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(true, "root", "cid", "parent", "cid", "url", "author", "text", "avatar"));
        var validator = new BlueskyContentValidator();
        var tool = new PublishPlatformTool(db, resolver, validator, replyResolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));


        Assert.True(result.Success);
        Assert.Equal(1, stubPublisher.CallCount);

        var createdThread = await db.PlatformThreads.FirstOrDefaultAsync(t => t.DraftId == draftId && t.Platform == "Bluesky");
        Assert.NotNull(createdThread);
        Assert.Equal("Autonomous thread content.", createdThread!.Content);
        Assert.Equal(PlatformThreadStage.Sent, createdThread.Stage);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsPublishedTextAndSetsThreadAccountId_WhenPublished()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing, Content = "Autonomous thread content." });
        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            Platform = "Bluesky",
            Handle = "user.bsky.social",
            CredentialsEncrypted = "encrypted"
        });
        await db.SaveChangesAsync();

        var stubPublisher = new StubPublisher();
        var resolver = new StubPublisherResolver(stubPublisher);
        var replyResolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(true, "root", "cid", "parent", "cid", "url", "author", "text", "avatar"));
        var validator = new BlueskyContentValidator();
        var tool = new PublishPlatformTool(db, resolver, validator, replyResolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        Assert.True(result.Success);

        var thread = await db.PlatformThreads.FirstOrDefaultAsync(t => t.DraftId == draftId);
        Assert.NotNull(thread);
        Assert.Equal(accountId, thread!.AccountId);

        var post = await db.Posts.FirstOrDefaultAsync(p => p.PlatformThreadId == thread.Id);
        Assert.NotNull(post);
        Assert.Equal("Live published text from platform", post!.PublishedText);
        Assert.Equal("user.bsky.social", post.AuthorHandle);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithDisambiguationError_WhenMultipleAccountsForPlatformAndNoneSpecified()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var account1Id = Guid.NewGuid();
        var account2Id = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing, Content = "Multi-account content." });
        db.Accounts.Add(new Account
        {
            Id = account1Id,
            UserId = userId,
            Platform = "Bluesky",
            Handle = "first.bsky.social",
            CredentialsEncrypted = "encrypted1"
        });
        db.Accounts.Add(new Account
        {
            Id = account2Id,
            UserId = userId,
            Platform = "Bluesky",
            Handle = "second.bsky.social",
            CredentialsEncrypted = "encrypted2"
        });
        await db.SaveChangesAsync();

        var stubPublisher = new StubPublisher();
        var resolver = new StubPublisherResolver(stubPublisher);
        var replyResolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(true, "root", "cid", "parent", "cid", "url", "author", "text", "avatar"));
        var validator = new BlueskyContentValidator();
        var tool = new PublishPlatformTool(db, resolver, validator, replyResolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        Assert.False(result.Success);
        Assert.Contains("Multiple accounts found", result.Error);
        Assert.Contains("first.bsky.social", result.Error);
        Assert.Contains("second.bsky.social", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Succeeds_WhenExplicitAccountIdSpecifiedAmongMultipleAccounts()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var account1Id = Guid.NewGuid();
        var account2Id = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing, Content = "Multi-account content." });
        db.Accounts.Add(new Account
        {
            Id = account1Id,
            UserId = userId,
            Platform = "Bluesky",
            Handle = "first.bsky.social",
            CredentialsEncrypted = "encrypted1"
        });
        db.Accounts.Add(new Account
        {
            Id = account2Id,
            UserId = userId,
            Platform = "Bluesky",
            Handle = "second.bsky.social",
            CredentialsEncrypted = "encrypted2"
        });
        await db.SaveChangesAsync();

        var stubPublisher = new StubPublisher();
        var resolver = new StubPublisherResolver(stubPublisher);
        var replyResolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(true, "root", "cid", "parent", "cid", "url", "author", "text", "avatar"));
        var validator = new BlueskyContentValidator();
        var tool = new PublishPlatformTool(db, resolver, validator, replyResolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("bluesky", account2Id), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        Assert.True(result.Success);

        var thread = await db.PlatformThreads.FirstOrDefaultAsync(t => t.DraftId == draftId);
        Assert.NotNull(thread);
        Assert.Equal(account2Id, thread!.AccountId);

        var post = await db.Posts.FirstOrDefaultAsync(p => p.PlatformThreadId == thread.Id);
        Assert.NotNull(post);
        Assert.Equal("second.bsky.social", post!.AuthorHandle);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsTwitterContent_WhenExceedingCharacterLimit()
    {
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var draftId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = userId, Username = "u", Email = "u@example.com", PasswordHash = "hash" });
        db.Drafts.Add(new Draft { Id = draftId, UserId = userId, Title = "Draft", Status = DraftStatus.Editing, Content = new string('a', 300) });
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "Twitter",
            Handle = "@testuser",
            CredentialsEncrypted = "encrypted"
        });
        await db.SaveChangesAsync();

        var stubPublisher = new StubPublisher();
        var resolver = new StubPublisherResolver(stubPublisher);
        var replyResolver = new StubReplyTargetResolver(new BlueskyReplyTargetResolutionResult(true, "root", "cid", "parent", "cid", "url", "author", "text", "avatar"));
        var validator = new BlueskyContentValidator();
        var draftValidatorResolver = new StubDraftValidatorResolver(new SocialWorker.Api.Features.Publishing.Twitter.Validation.TwitterDraftValidator());
        var tool = new PublishPlatformTool(db, resolver, validator, replyResolver, draftValidatorResolver);

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Twitter"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        Assert.False(result.Success);
        Assert.Contains("exceeds the 280 weighted-character limit", result.Error);
    }

    private sealed class StubDraftValidatorResolver : SocialWorker.Api.Features.Publishing.Validation.IDraftValidatorResolver
    {
        private readonly SocialWorker.Api.Features.Publishing.Validation.IDraftValidator _validator;
        public StubDraftValidatorResolver(SocialWorker.Api.Features.Publishing.Validation.IDraftValidator validator) => _validator = validator;
        public SocialWorker.Api.Features.Publishing.Validation.IDraftValidator Resolve(SocialPlatform? platform) => _validator;
    }
}