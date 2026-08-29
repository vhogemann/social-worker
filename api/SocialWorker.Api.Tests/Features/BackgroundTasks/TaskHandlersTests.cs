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

        public FakeTranscriber(string dir)
        {
            _dir = dir;
        }

        public async Task<TranscriptExtractionResult> ExtractAsync(string mediaUrl, string outputPath, CancellationToken ct)
        {
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
