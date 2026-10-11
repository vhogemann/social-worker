using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.BackgroundTasks.Services;
using SocialWorker.Api.Features.Drafts.Handlers;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Feeds.Handlers;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Features.Sources.Handlers;
using Xunit;

namespace SocialWorker.Api.Tests.Features.BackgroundTasks;

public sealed class TaskHandlersTests : SqliteTestBase
{
    [Fact]
    public async Task SourceScrapeTaskHandler_ScrapesUrlsAndUpdatesDraft()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Draft" };
        var source = Source.CreateUrl("https://example.com/article", "Fetching...");
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        await db.SaveChangesAsync();

        var html = "<html><head><title>Scraped Title</title></head><body><main><p>Scraped article text</p></main></body></html>";
        var scraper = new WebScraperService(new HttpClient(new SimpleHandler(html)));

        var handler = new SourceScrapeTaskHandler(db, scraper);
        var queue = new TestBackgroundTaskQueue();
        var task = BackgroundTask.Create("source-scrape", new SourceScrapePayload(draft.Id, new List<Guid> { source.Id }));
        var context = new TaskExecutionContext(task, "worker", queue, CancellationToken.None);

        var result = await handler.ExecuteAsync(new SourceScrapePayload(draft.Id, new List<Guid> { source.Id }), context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        var updatedSource = await db.Sources.FindAsync(source.Id);
        Assert.Equal("Scraped Title", updatedSource!.Title);
        Assert.Contains("Scraped article text", updatedSource.Content);
    }

    [Fact]
    public async Task SourceTranscriptionTaskHandler_ExtractsTranscriptAndCompletesSource()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Draft" };
        var source = Source.CreateYouTube("vid123", "https://youtube.com/watch?v=vid123", "Fetching...");
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        await db.SaveChangesAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), $"transcripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeTranscriber = new FakeTranscriber(tempDir);
            var handler = new SourceTranscriptionTaskHandler(
                db,
                fakeTranscriber,
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<SourceTranscriptionTaskHandler>.Instance);

            var queue = new TestBackgroundTaskQueue();
            var task = BackgroundTask.Create("source-transcription", new SourceTranscriptionPayload(source.Id, draft.Id));
            var context = new TaskExecutionContext(task, "worker", queue, CancellationToken.None);

            var result = await handler.ExecuteAsync(new SourceTranscriptionPayload(source.Id, draft.Id), context, CancellationToken.None);

            Assert.Equal(TaskOutcome.Success, result.Outcome);
            var updatedSource = await db.Sources.FindAsync(source.Id);
            Assert.Equal(SourceProcessingStatus.Complete, updatedSource!.ProcessingStatus);
            Assert.Equal("Extracted video transcript", updatedSource.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SourceTranscriptionTaskHandler_WithMediaUrl_PassesMediaUrlToTranscriber_AndCombinesWithExistingCaption()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Draft" };
        var source = Source.CreateUrl("https://www.instagram.com/p/DB12345", "Reel");
        source.Content = "Original Reel caption from politician";
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        await db.SaveChangesAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), $"transcripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeTranscriber = new FakeTranscriber(tempDir);
            var handler = new SourceTranscriptionTaskHandler(
                db,
                fakeTranscriber,
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<SourceTranscriptionTaskHandler>.Instance);

            var queue = new TestBackgroundTaskQueue();
            var payload = new SourceTranscriptionPayload(source.Id, draft.Id, "https://cdn.example.com/video.mp4");
            var task = BackgroundTask.Create("source-transcription", payload);
            var context = new TaskExecutionContext(task, "worker", queue, CancellationToken.None);

            var result = await handler.ExecuteAsync(payload, context, CancellationToken.None);

            Assert.Equal(TaskOutcome.Success, result.Outcome);
            Assert.Equal("https://cdn.example.com/video.mp4", fakeTranscriber.LastRequestedUrl);

            var updatedSource = await db.Sources.FindAsync(source.Id);
            Assert.Equal(SourceProcessingStatus.Complete, updatedSource!.ProcessingStatus);
            Assert.Contains("Original Reel caption from politician", updatedSource.Content);
            Assert.Contains("[Transcrição do Vídeo (Whisper)]", updatedSource.Content);
            Assert.Contains("Extracted video transcript", updatedSource.Content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FeedIngestionTaskHandler_SubscriptionNotFound_ReturnsFatal()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var orchestrator = new FakeOrchestrationService();
        var handler = new FeedIngestionTaskHandler(db, orchestrator, NullLogger<FeedIngestionTaskHandler>.Instance);

        var payload = new FeedIngestionPayload(Guid.NewGuid(), workflow.Id, "Title", "https://example.com/link", "Desc", null);
        var task = BackgroundTask.Create("feed-ingestion", payload);
        var context = new TaskExecutionContext(task, "worker", new TestBackgroundTaskQueue(), CancellationToken.None);

        var result = await handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Fatal, result.Outcome);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task FeedIngestionTaskHandler_WorkflowNotFound_ReturnsFatal()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        db.FeedSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var orchestrator = new FakeOrchestrationService();
        var handler = new FeedIngestionTaskHandler(db, orchestrator, NullLogger<FeedIngestionTaskHandler>.Instance);

        var payload = new FeedIngestionPayload(sub.Id, Guid.NewGuid(), "Title", "https://example.com/link", "Desc", null);
        var task = BackgroundTask.Create("feed-ingestion", payload);
        var context = new TaskExecutionContext(task, "worker", new TestBackgroundTaskQueue(), CancellationToken.None);

        var result = await handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Fatal, result.Outcome);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task FeedIngestionTaskHandler_SuccessfulExecution_ReturnsSuccess()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        db.FeedSubscriptions.Add(sub);
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var orchestrator = new FakeOrchestrationService();
        var handler = new FeedIngestionTaskHandler(db, orchestrator, NullLogger<FeedIngestionTaskHandler>.Instance);

        var payload = new FeedIngestionPayload(sub.Id, workflow.Id, "Title", "https://example.com/link", "Desc", null);
        var task = BackgroundTask.Create("feed-ingestion", payload);
        var context = new TaskExecutionContext(task, "worker", new TestBackgroundTaskQueue(), CancellationToken.None);

        var result = await handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Success, result.Outcome);
        Assert.True(orchestrator.ProcessCalled);
    }

    [Fact]
    public async Task FeedIngestionTaskHandler_OrchestratorThrows_ReturnsRetryWithBackoff()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        db.FeedSubscriptions.Add(sub);
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var orchestrator = new FakeOrchestrationService { ThrowOnProcess = true };
        var handler = new FeedIngestionTaskHandler(db, orchestrator, NullLogger<FeedIngestionTaskHandler>.Instance);

        var payload = new FeedIngestionPayload(sub.Id, workflow.Id, "Title", "https://example.com/link", "Desc", null);
        var task = BackgroundTask.Create("feed-ingestion", payload);
        var context = new TaskExecutionContext(task, "worker", new TestBackgroundTaskQueue(), CancellationToken.None);

        var result = await handler.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.Equal(TaskOutcome.Retry, result.Outcome);
        Assert.NotNull(result.RetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), result.RetryDelay);
    }

    private sealed class FakeOrchestrationService : FeedOrchestrationService
    {
        public bool ProcessCalled { get; private set; }
        public bool ThrowOnProcess { get; set; }

        public FakeOrchestrationService()
            : base(null!, null!, null!, null!, null!, NullLogger<FeedOrchestrationService>.Instance)
        {
        }

        public override Task ProcessFeedItemAsync(
            FeedSubscription subscription,
            AutomationWorkflow workflow,
            string itemTitle,
            string itemLink,
            string itemDescription,
            DateTime? itemPublishDate,
            CancellationToken ct)
        {
            ProcessCalled = true;
            if (ThrowOnProcess)
            {
                throw new InvalidOperationException("Orchestration processing failure");
            }
            return Task.CompletedTask;
        }

        public override Task ProcessFeedItemAsync(
            FeedSubscription subscription,
            AutomationWorkflow workflow,
            string itemTitle,
            string itemLink,
            string itemDescription,
            DateTime? itemPublishDate,
            CancellationToken ct,
            string? mediaUrl,
            string? mediaType)
        {
            return ProcessFeedItemAsync(subscription, workflow, itemTitle, itemLink, itemDescription, itemPublishDate, ct);
        }
    }

    private sealed class SimpleHandler : HttpMessageHandler
    {
        private readonly string _html;

        public SimpleHandler(string html)
        {
            _html = html;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, Encoding.UTF8, "text/html")
            });
        }
    }

    private sealed class FakeTranscriber : ITranscriptExtractionService
    {
        private readonly string _dir;
        public string? LastRequestedUrl { get; private set; }

        public FakeTranscriber(string dir)
        {
            _dir = dir;
        }

        public async Task<TranscriptExtractionResult> ExtractAsync(string mediaUrl, string outputPath, CancellationToken ct)
        {
            LastRequestedUrl = mediaUrl;
            var path = Path.Combine(_dir, outputPath);
            await File.WriteAllTextAsync(path, "{\"transcript\":\"Extracted video transcript\"}", ct);
            return new TranscriptExtractionResult(true, outputPath, 60, "en", null);
        }

        public async Task<TranscriptDocument?> ReadTranscriptAsync(string transcriptPath, CancellationToken ct)
        {
            var fullPath = Path.Combine(_dir, transcriptPath);
            if (!File.Exists(fullPath)) return null;
            var json = await File.ReadAllTextAsync(fullPath, ct);
            return System.Text.Json.JsonSerializer.Deserialize<TranscriptDocument>(json,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }

        public Task<bool> HealthAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }
    }
}
