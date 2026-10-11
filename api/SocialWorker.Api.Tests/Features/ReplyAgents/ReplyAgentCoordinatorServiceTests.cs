using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class ReplyAgentCoordinatorServiceTests : SqliteTestBase
{
    private sealed class FakePlatformAdapter : IReplyPlatformAdapter
    {
        public string Platform => "Bluesky";
        public List<PlatformCommentItem> CommentsToReturn { get; set; } = new();
        public PublishReplyResult PublishResultToReturn { get; set; } = PublishReplyResult.Succeeded("new-reply-id-123");
        public int PublishCallCount { get; private set; }

        public Task<IReadOnlyList<PlatformCommentItem>> FetchCommentsAsync(
            Post post,
            Account account,
            DateTime since,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformCommentItem>>(CommentsToReturn);
        }

        public Task<PublishReplyResult> PublishReplyAsync(
            Post post,
            Account account,
            PlatformCommentItem targetComment,
            string replyText,
            CancellationToken ct = default)
        {
            PublishCallCount++;
            return Task.FromResult(PublishResultToReturn);
        }
    }

    private sealed class FakeAdapterResolver : IReplyPlatformAdapterResolver
    {
        private readonly IReplyPlatformAdapter _adapter;
        public FakeAdapterResolver(IReplyPlatformAdapter adapter) => _adapter = adapter;
        public IReplyPlatformAdapter? Resolve(string platform) => _adapter;
        public IReadOnlyCollection<string> SupportedPlatforms => new[] { "Bluesky" };
    }

    private sealed class FakeEnrichmentService : ICommentEnrichmentService
    {
        public Task<EnrichedCommentContext> EnrichCommentAsync(
            PlatformCommentItem comment,
            AppUser user,
            CancellationToken ct = default)
        {
            return Task.FromResult(new EnrichedCommentContext(
                comment,
                Array.Empty<ParsedLinkContext>(),
                Array.Empty<ParsedImageContext>(),
                $"Comment: {comment.Text}"
            ));
        }
    }

    private sealed class FakeRankingService : ICommentRankingService
    {
        public CommentRankingResult ResultToReturn { get; set; } = new(8, "GenuineQuestion", true, false, "Good question");

        public Task<CommentRankingResult> RankCommentAsync(
            EnrichedCommentContext enrichedContext,
            string originalPostContent,
            string? threadSummary,
            AppUser user,
            CancellationToken ct = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeReplyGenerator : IReplyGenerationService
    {
        public GeneratedReplyResult ResultToReturn { get; set; } = GeneratedReplyResult.Succeeded("Helpful generated answer");

        public Task<GeneratedReplyResult> GenerateReplyAsync(
            EnrichedCommentContext enrichedContext,
            string originalPostContent,
            string personaPrompt,
            string platform,
            string? threadSummary,
            AppUser user,
            CancellationToken ct = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeSummarizer : ICommentThreadSummarizer
    {
        public Task<string?> CompactThreadAsync(
            string? existingSummary,
            IReadOnlyList<PlatformCommentItem> unsummarizedComments,
            AppUser user,
            CancellationToken ct = default)
        {
            return Task.FromResult<string?>("Compacted summary");
        }
    }

    private (AutomationWorkflow Workflow, ReplyAgent Agent, Post Post, Account Account) SeedScenario(
        AppDbContext db,
        bool isAgentEnabled = true,
        bool autoPublish = true,
        int minScore = 7,
        int maxRepliesPerBranch = 3,
        int maxRepliesPerUser = 2,
        int maxDepth = 5,
        int maxAgeHours = 48)
    {
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "Tech Feed", "Feed workflow");
        db.AutomationWorkflows.Add(workflow);

        var agent = ReplyAgent.Create(
            user.Id,
            name: "Tech Agent",
            personaPrompt: "Polite tech assistant",
            maxCommentAgeHours: maxAgeHours,
            maxPostAgeDays: 7,
            minRankingScore: minScore,
            maxRepliesPerCommentThread: maxRepliesPerBranch,
            maxRepliesPerUser: maxRepliesPerUser,
            maxThreadDepth: maxDepth,
            autoPublishReplies: autoPublish
        );
        agent.IsEnabled = isAgentEnabled;
        db.ReplyAgents.Add(agent);

        workflow.ReplyAgentId = agent.Id;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Bluesky",
            Handle = "bot.bsky.social",
            CredentialsEncrypted = "dummy-encrypted"
        };
        db.Accounts.Add(account);

        var draft = Draft.Create(user.Id, "Test Draft");
        db.Drafts.Add(draft);

        var pt = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            AccountId = account.Id,
            Account = account
        };
        db.PlatformThreads.Add(pt);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Draft = draft,
            Platform = "Bluesky",
            RemoteId = "at://did:plc:bot/app.bsky.feed.post/123",
            PublishedText = "Hello world from the bot!",
            PlatformThreadId = pt.Id,
            PlatformThread = pt
        };
        db.Posts.Add(post);
        db.SaveChanges();

        return (workflow, agent, post, account);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_AgentDisabled_DoesNothing()
    {
        using var db = CreateDbContext();
        var (_, agent, post, _) = SeedScenario(db, isAgentEnabled: false);

        var adapter = new FakePlatformAdapter();
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c1", null, post.RemoteId, "user.bsky.social", "did:user", "Question", DateTime.UtcNow, 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), new FakeRankingService(),
            new FakeReplyGenerator(), new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        Assert.Equal(0, adapter.PublishCallCount);
        Assert.Empty(db.WorkflowCommentAudits);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_SelfReply_IgnoredWithoutAudit()
    {
        using var db = CreateDbContext();
        var (_, agent, post, account) = SeedScenario(db);

        var adapter = new FakePlatformAdapter();
        // Comment from the bot's own handle
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c1", null, post.RemoteId, account.Handle, "did:bot", "My own comment", DateTime.UtcNow, 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), new FakeRankingService(),
            new FakeReplyGenerator(), new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        Assert.Equal(0, adapter.PublishCallCount);
        Assert.Empty(db.WorkflowCommentAudits);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_CommentTooOld_AuditedAsSkippedTooOld()
    {
        using var db = CreateDbContext();
        var (_, agent, post, _) = SeedScenario(db, maxAgeHours: 24);

        var adapter = new FakePlatformAdapter();
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c_old", null, post.RemoteId, "user1.bsky.social", "did:u1", "Old question", DateTime.UtcNow.AddHours(-30), 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), new FakeRankingService(),
            new FakeReplyGenerator(), new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        Assert.Equal(0, adapter.PublishCallCount);
        var audit = Assert.Single(db.WorkflowCommentAudits);
        Assert.Equal("SkippedTooOld", audit.RankingDecision);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_MaxThreadDepthExceeded_AuditedAsSkippedStopCriteria()
    {
        using var db = CreateDbContext();
        var (_, agent, post, _) = SeedScenario(db, maxDepth: 3);

        var adapter = new FakePlatformAdapter();
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c_deep", "p_deep", post.RemoteId, "user1.bsky.social", "did:u1", "Deep comment", DateTime.UtcNow, 4, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), new FakeRankingService(),
            new FakeReplyGenerator(), new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        Assert.Equal(0, adapter.PublishCallCount);
        var audit = Assert.Single(db.WorkflowCommentAudits);
        Assert.Equal("SkippedStopCriteria", audit.RankingDecision);
        Assert.Contains("MaxThreadDepth exceeded", audit.RankingReasoning);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_TerminalClosedComment_MarksThreadTerminal()
    {
        using var db = CreateDbContext();
        var (_, agent, post, _) = SeedScenario(db);

        var adapter = new FakePlatformAdapter();
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c_term", null, post.RemoteId, "user1.bsky.social", "did:u1", "Thanks, that solved it!", DateTime.UtcNow, 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var rankingService = new FakeRankingService
        {
            ResultToReturn = new CommentRankingResult(4, "TerminalClosure", false, true, "User thanked and closed issue")
        };

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), rankingService,
            new FakeReplyGenerator(), new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        Assert.Equal(0, adapter.PublishCallCount);
        var audit = Assert.Single(db.WorkflowCommentAudits);
        Assert.Equal("SkippedTerminal", audit.RankingDecision);

        var thread = Assert.Single(db.WorkflowCommentThreads);
        Assert.True(thread.IsTerminal);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_HighScore_AutoPublishFalse_AuditedAsPendingApproval()
    {
        using var db = CreateDbContext();
        var (_, agent, post, _) = SeedScenario(db, autoPublish: false);

        var adapter = new FakePlatformAdapter();
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c_good", null, post.RemoteId, "user1.bsky.social", "did:u1", "Can you explain step 2?", DateTime.UtcNow, 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var rankingService = new FakeRankingService
        {
            ResultToReturn = new CommentRankingResult(9, "GenuineQuestion", true, false, "High quality question")
        };
        var replyGenerator = new FakeReplyGenerator
        {
            ResultToReturn = GeneratedReplyResult.Succeeded("Step 2 requires running docker-compose up.")
        };

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), rankingService,
            replyGenerator, new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        // AutoPublish is false, so it shouldn't call PublishReplyAsync
        Assert.Equal(0, adapter.PublishCallCount);

        var audit = Assert.Single(db.WorkflowCommentAudits);
        Assert.Equal("PendingApproval", audit.RankingDecision);
        Assert.Equal("Step 2 requires running docker-compose up.", audit.GeneratedReplyText);
    }

    [Fact]
    public async Task ProcessPostCommentsAsync_HighScore_AutoPublishTrue_PublishesAndAuditsAsReplied()
    {
        using var db = CreateDbContext();
        var (_, agent, post, _) = SeedScenario(db, autoPublish: true);

        var adapter = new FakePlatformAdapter();
        adapter.CommentsToReturn.Add(new PlatformCommentItem(
            "Bluesky", "c_good", null, post.RemoteId, "user1.bsky.social", "did:u1", "Can you explain step 2?", DateTime.UtcNow, 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()));

        var rankingService = new FakeRankingService
        {
            ResultToReturn = new CommentRankingResult(9, "GenuineQuestion", true, false, "High quality question")
        };
        var replyGenerator = new FakeReplyGenerator
        {
            ResultToReturn = GeneratedReplyResult.Succeeded("Step 2 requires running docker-compose up.")
        };

        var coordinator = new ReplyAgentCoordinatorService(
            db, new FakeAdapterResolver(adapter), new FakeEnrichmentService(), rankingService,
            replyGenerator, new FakeSummarizer(), new TestBackgroundTaskQueue(), NullLogger<ReplyAgentCoordinatorService>.Instance);

        await coordinator.ProcessPostCommentsAsync(agent.Id, post.Id);

        Assert.Equal(1, adapter.PublishCallCount);

        var audit = Assert.Single(db.WorkflowCommentAudits);
        Assert.Equal("Replied", audit.RankingDecision);
        Assert.Equal("new-reply-id-123", audit.RemoteReplyId);
        Assert.Equal("Step 2 requires running docker-compose up.", audit.GeneratedReplyText);

        var thread = Assert.Single(db.WorkflowCommentThreads);
        Assert.Equal(1, thread.AgentReplyCount);
    }
}
