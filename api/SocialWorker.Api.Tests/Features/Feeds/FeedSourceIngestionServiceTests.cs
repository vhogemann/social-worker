using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedSourceIngestionServiceTests : SqliteTestBase
{
    [Theory]
    [InlineData("Tech News", "Some tech description", null, null, true)]
    [InlineData("Tech News", "Some tech description", "Tech", null, true)]
    [InlineData("Sports News", "Some sports description", "Tech", null, false)]
    [InlineData("Tech News", "Contains Crypto scam", null, "Crypto", false)]
    [InlineData("Tech News", "Normal post", "Tech", "Crypto", true)]
    public void PassesFilters_EvaluatesIncludesAndExcludesCorrectly(
        string title,
        string description,
        string? includeFilters,
        string? excludeFilters,
        bool expected)
    {
        var result = FeedSourceIngestionService.PassesFilters(title, description, includeFilters, excludeFilters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IngestSourceAsync_WhenItemMatchesExistingSourceForSameWorkflow_ReturnsDuplicate()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        db.AutomationWorkflows.Add(workflow);
        db.FeedSubscriptions.Add(sub);

        var existingDraft = Draft.Create(user.Id, "Existing Draft");
        existingDraft.AutomationWorkflowId = workflow.Id;
        existingDraft.Status = DraftStatus.Editing;
        var existingSource = Source.CreateUrl("https://example.com/article1", "Title");
        db.Drafts.Add(existingDraft);
        db.Sources.Add(existingSource);
        db.DraftSources.Add(new DraftSource { DraftId = existingDraft.Id, SourceId = existingSource.Id });
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var service = new FeedSourceIngestionService(db, sourcesService, NullLogger<FeedSourceIngestionService>.Instance);

        var result = await service.IngestSourceAsync(sub, workflow, "New Title", "https://example.com/article1", "Desc", CancellationToken.None);

        Assert.Equal(IngestionStatus.Duplicate, result.Status);
        Assert.Null(result.Draft);
    }

    [Fact]
    public async Task IngestSourceAsync_WhenItemFailsFilters_ReturnsFilteredOut()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        var sub = new FeedSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Feed",
            FeedUrl = "https://example.com/feed",
            IncludeFilters = "AI,Tech",
            ExcludeFilters = "Crypto"
        };
        db.AutomationWorkflows.Add(workflow);
        db.FeedSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var service = new FeedSourceIngestionService(db, sourcesService, NullLogger<FeedSourceIngestionService>.Instance);

        var result = await service.IngestSourceAsync(sub, workflow, "Football Match", "https://example.com/sports", "A match occurred", CancellationToken.None);

        Assert.Equal(IngestionStatus.FilteredOut, result.Status);
        Assert.Null(result.Draft);
    }

    [Fact]
    public async Task IngestSourceAsync_WhenSourcesServiceFails_MarksDraftFailedAndReturnsFailed()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        db.AutomationWorkflows.Add(workflow);
        db.FeedSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var failingHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var scraper = new WebScraperService(new HttpClient(failingHandler));
        var sourcesService = TestServiceFactory.CreateSourcesService(db, scraper: scraper);
        var service = new FeedSourceIngestionService(db, sourcesService, NullLogger<FeedSourceIngestionService>.Instance);

        var result = await service.IngestSourceAsync(sub, workflow, "Broken Article", "https://example.com/error-page", "Desc", CancellationToken.None);

        Assert.Equal(IngestionStatus.Failed, result.Status);
        Assert.NotNull(result.Draft);
        Assert.Equal(DraftStatus.Failed, result.Draft!.Status);
        Assert.StartsWith("[Failed Ingestion]", result.Draft.Title);
    }

    [Fact]
    public async Task IngestSourceAsync_WhenUrlSourceSucceeds_CreatesDraftWithWorkflowIdAndReturnsSuccess()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        db.AutomationWorkflows.Add(workflow);
        db.FeedSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var html = "<html><head><title>Clean Article</title></head><body><p>Article body content</p></body></html>";
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        });
        var scraper = new WebScraperService(new HttpClient(handler));
        var sourcesService = TestServiceFactory.CreateSourcesService(db, scraper: scraper);
        var service = new FeedSourceIngestionService(db, sourcesService, NullLogger<FeedSourceIngestionService>.Instance);

        var result = await service.IngestSourceAsync(sub, workflow, "Clean Article", "https://example.com/article-success", "Desc", CancellationToken.None);

        Assert.Equal(IngestionStatus.Success, result.Status);
        Assert.NotNull(result.Draft);
        Assert.Equal(workflow.Id, result.Draft!.AutomationWorkflowId);
        Assert.Equal(user.Id, result.Draft.UserId);
        Assert.NotNull(result.SourceId);
    }

    [Fact]
    public async Task IngestSourceAsync_WhenYouTubeSource_ReturnsPendingTranscriptionImmediately()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        var sub = new FeedSubscription { Id = Guid.NewGuid(), UserId = user.Id, Title = "Feed", FeedUrl = "https://example.com/feed" };
        db.AutomationWorkflows.Add(workflow);
        db.FeedSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var service = new FeedSourceIngestionService(db, sourcesService, NullLogger<FeedSourceIngestionService>.Instance);

        var result = await service.IngestSourceAsync(sub, workflow, "YT Video", "https://youtube.com/watch?v=xyz987", "Desc", CancellationToken.None);

        Assert.Equal(IngestionStatus.PendingTranscription, result.Status);
        Assert.NotNull(result.Draft);
        Assert.Equal(DraftStatus.Editing, result.Draft!.Status);
        Assert.NotNull(result.SourceId);

        var ytSource = await db.Sources.FirstOrDefaultAsync(s => s.Reference == "https://youtube.com/watch?v=xyz987");
        Assert.NotNull(ytSource);
        Assert.Equal(SourceKind.YouTube, ytSource!.Kind);
    }

    [Fact]
    public async Task IngestSourceAsync_WhenInstagramVideo_ReturnsPendingTranscriptionAndEnqueuesTask()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "WF", "Prompt");
        var sub = new FeedSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Instagram Profile",
            FeedUrl = "instagram://flaviobolsonaro"
        };
        db.AutomationWorkflows.Add(workflow);
        db.FeedSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var queue = new TestBackgroundTaskQueue();
        var transcriptionService = new SourceTranscriptionService(queue);
        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var service = new FeedSourceIngestionService(db, sourcesService, NullLogger<FeedSourceIngestionService>.Instance, transcriptionService);

        var videoUrl = "https://cdn.example.com/instagram/reel.mp4";
        var permalink = "https://www.instagram.com/p/DB12345";
        var result = await service.IngestSourceAsync(
            sub,
            workflow,
            "Reel Title",
            permalink,
            "Reel caption text",
            CancellationToken.None,
            mediaUrl: videoUrl,
            mediaType: "VIDEO");

        Assert.Equal(IngestionStatus.PendingTranscription, result.Status);
        Assert.NotNull(result.Draft);
        Assert.NotNull(result.SourceId);

        var queuedTask = Assert.Single(queue.EnqueuedTasks);
        Assert.Equal("source-transcription", queuedTask.TaskType);
        var payload = System.Text.Json.JsonSerializer.Deserialize<SocialWorker.Api.Features.Sources.Handlers.SourceTranscriptionPayload>(
            queuedTask.PayloadJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(result.SourceId.Value, payload!.SourceId);
        Assert.Equal(result.Draft!.Id, payload.DraftId);
        Assert.Equal(videoUrl, payload.MediaUrl);

        var savedSource = await db.Sources.FindAsync(result.SourceId.Value);
        Assert.NotNull(savedSource);
        Assert.Equal(permalink, savedSource!.Reference);
        Assert.Equal("Reel caption text", savedSource.Content);
    }
}
