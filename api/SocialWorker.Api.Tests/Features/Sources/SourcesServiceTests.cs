using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Background;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SocialWorker.Api.Tests;

public sealed class SourcesServiceTests : SqliteTestBase
{
    [Fact]
    public async Task AddUrlSourceAsync_Adds_Source_With_Scraped_Content()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var html = "<html><head><title>Example Page</title></head><body><main><p>Hello world content.</p></main></body></html>";
        var scraper = new WebScraperService(new HttpClient(new StaticHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
            return response;
        })));

        var service = new SourcesService(db, scraper, null!, null!);
        var result = await service.AddUrlSourceAsync(
            userId,
            draft.Id,
            "https://example.com/post",
            null,
            null,
            CancellationToken.None);

        Assert.Equal("Url", result.Kind);
        Assert.Equal("https://example.com/post", result.Reference);

        var created = await db.Sources.FirstOrDefaultAsync(s => s.Id == result.SourceId);
        Assert.NotNull(created);
        Assert.Equal(SourceKind.Url, created!.Kind);
        Assert.Contains("Hello world", created.Content ?? string.Empty);
    }

    [Fact]
    public async Task AddUrlSourceAsync_Throws_For_Invalid_Url()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var scraper = new WebScraperService(new HttpClient(new StaticHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") })));

        var service = new SourcesService(db, scraper, null!, null!);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddUrlSourceAsync(userId, draft.Id, "not-a-url", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetSourceDetailAsync_Throws_For_AccessDenied_Or_NotFound()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Title" };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        // Access denied (wrong user ID)
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSourceDetailAsync(Guid.NewGuid(), draft.Id, Guid.NewGuid(), CancellationToken.None));

        // Source not found
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSourceDetailAsync(userId, draft.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetSourceDetailAsync_Returns_DetailDto_Successfully()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.Url,
            Reference = "https://example.com/source",
            Title = "Source Title",
            Content = "Cached body text here"
        };
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        var result = await service.GetSourceDetailAsync(userId, draft.Id, source.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal(draft.Id, result.DraftId);
        Assert.Equal("Url", result.Kind);
        Assert.Equal("https://example.com/source", result.Reference);
        Assert.Equal("Source Title", result.Title);
        Assert.Equal("Cached body text here", result.Content);
    }

    [Fact]
    public async Task DeleteSourceAsync_Deletes_Source_Successfully()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.Url,
            Reference = "https://example.com/source",
            Title = "Source Title"
        };
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        await service.DeleteSourceAsync(userId, draft.Id, source.Id, CancellationToken.None);

        var deleted = await db.Sources.FirstOrDefaultAsync(s => s.Id == source.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteSourceAsync_Unlinks_Source_But_Keeps_Shared_Source()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        db.Users.AddRange(
            new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" },
            new AppUser { Id = otherUserId, Username = "other", Email = "other@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        var otherDraft = new Draft { Id = Guid.NewGuid(), UserId = otherUserId, Title = "Other draft" };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.Url,
            Reference = "https://example.com/source",
            Title = "Source Title"
        };
        db.Drafts.AddRange(draft, otherDraft);
        db.Sources.Add(source);
        db.DraftSources.AddRange(
            new DraftSource { DraftId = draft.Id, SourceId = source.Id },
            new DraftSource { DraftId = otherDraft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        await service.DeleteSourceAsync(userId, draft.Id, source.Id, CancellationToken.None);

        var kept = await db.Sources.FirstOrDefaultAsync(s => s.Id == source.Id);
        Assert.NotNull(kept);
        Assert.False(await db.DraftSources.AnyAsync(ds => ds.DraftId == draft.Id && ds.SourceId == source.Id));
        Assert.True(await db.DraftSources.AnyAsync(ds => ds.DraftId == otherDraft.Id && ds.SourceId == source.Id));
    }

    [Fact]
    public async Task SearchSourcesAsync_Returns_Only_UserAccessible_Sources()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        db.Users.AddRange(
            new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" },
            new AppUser { Id = otherUserId, Username = "other", Email = "other@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        var otherDraft = new Draft { Id = Guid.NewGuid(), UserId = otherUserId, Title = "Other draft" };
        var source1 = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://example.com/alpha", Title = "Alpha notes", Summary = "alpha summary" };
        var source2 = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://example.com/beta", Title = "Beta notes" };
        db.Drafts.AddRange(draft, otherDraft);
        db.Sources.AddRange(source1, source2);
        db.DraftSources.AddRange(
            new DraftSource { DraftId = draft.Id, SourceId = source1.Id },
            new DraftSource { DraftId = otherDraft.Id, SourceId = source2.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        var result = await service.SearchSourcesAsync(userId, "alpha", 1, 20, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(source1.Id, result.Items[0].Id);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task LinkSourceAsync_Creates_Link_For_Existing_Source()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var originalDraft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Original draft" };
        var targetDraft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Target draft" };
        var source = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://example.com/source", Title = "Source Title" };
        db.Drafts.AddRange(originalDraft, targetDraft);
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = originalDraft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        var result = await service.LinkSourceAsync(userId, source.Id, targetDraft.Id, CancellationToken.None);

        Assert.Equal(targetDraft.Id, result.DraftId);
        Assert.True(await db.DraftSources.AnyAsync(ds => ds.DraftId == targetDraft.Id && ds.SourceId == source.Id));
    }

    [Fact]
    public async Task GetSourceStatusAsync_Returns_Transcript_Metadata()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.YouTube,
            Reference = "https://youtube.com/watch?v=abc123xyz09",
            Title = "Video",
            Summary = "Transcript summary",
            TranscriptStatus = TranscriptStatus.Processing,
            YoutubeVideoId = "abc123xyz09"
        };
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        var result = await service.GetSourceStatusAsync(userId, source.Id, CancellationToken.None);

        Assert.Equal("Processing", result.TranscriptStatus);
        Assert.Equal("Transcript summary", result.Summary);
        Assert.Equal("abc123xyz09", result.YoutubeVideoId);
    }

    [Fact]
    public async Task RetrySourceTranscriptAsync_Queues_New_Transcription_And_Updates_Source()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.YouTube,
            Reference = "https://www.youtube.com/watch?v=abc123xyz09",
            Title = "Video",
            TranscriptStatus = TranscriptStatus.Failed,
            Summary = "Previous failure",
            YoutubeVideoId = "abc123xyz09"
        };
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id, LinkedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var queue = new BackgroundJobQueue();
        var transcriptDir = Path.Combine(Path.GetTempPath(), $"sw-transcripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(transcriptDir);
        try
        {
            var transcriptService = new FakeTranscriptExtractionService(transcriptDir);
            var scopeFactory = new TestServiceScopeFactory(db, transcriptService);
            var service = new SourcesService(db, null!, scopeFactory, queue);

            var status = await service.RetrySourceTranscriptAsync(userId, source.Id, CancellationToken.None);
            Assert.Equal("Pending", status.TranscriptStatus);

            using var queueReadCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queuedJob = await queue.ReadAsync(queueReadCts.Token);
            await queuedJob.Work(CancellationToken.None);

            var updated = await db.Sources.FirstAsync(s => s.Id == source.Id);
            Assert.Equal(TranscriptStatus.Complete, updated.TranscriptStatus);
            Assert.Equal("Full transcript body", updated.Content);
            Assert.Equal($"{source.Id}.json", updated.TranscriptPath);
        }
        finally
        {
            if (Directory.Exists(transcriptDir))
            {
                Directory.Delete(transcriptDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddUrlSourceAsync_For_YouTube_Queues_Transcript_Extraction_And_Stores_Result()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var transcriptDir = Path.Combine(Path.GetTempPath(), $"sw-transcripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(transcriptDir);
        try
        {
            var scraper = new WebScraperService(new HttpClient(new StaticHttpMessageHandler(_ =>
            {
                var payload = "{\"title\":\"Example Video\",\"author_name\":\"Example Author\",\"author_url\":\"https://youtube.com/@example\"}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            })));

            var queue = new BackgroundJobQueue();
            var transcriptService = new FakeTranscriptExtractionService(transcriptDir);
            var scopeFactory = new TestServiceScopeFactory(db, transcriptService);
            var service = new SourcesService(db, scraper, scopeFactory, queue);

            var result = await service.AddUrlSourceAsync(
                userId,
                draft.Id,
                "https://www.youtube.com/watch?v=abc123xyz09",
                null,
                null,
                CancellationToken.None);

            using var queueReadCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queuedJob = await queue.ReadAsync(queueReadCts.Token);
            await queuedJob.Work(CancellationToken.None);

            var source = await db.Sources.FirstAsync(s => s.Id == result.SourceId);
            Assert.Equal(SourceKind.YouTube, source.Kind);
            Assert.Equal(TranscriptStatus.Complete, source.TranscriptStatus);
            Assert.Equal("abc123xyz09", source.YoutubeVideoId);
            Assert.Equal("Full transcript body", source.Content);
            Assert.Equal($"{source.Id}.json", source.TranscriptPath);
        }
        finally
        {
            if (Directory.Exists(transcriptDir))
            {
                Directory.Delete(transcriptDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddUrlSourceAsync_For_YouTubeShorts_Queues_Transcript_Extraction_And_Stores_Result()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft title" };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var transcriptDir = Path.Combine(Path.GetTempPath(), $"sw-transcripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(transcriptDir);
        try
        {
            var scraper = new WebScraperService(new HttpClient(new StaticHttpMessageHandler(req =>
            {
                var url = req.RequestUri?.OriginalString ?? string.Empty;
                if (url.Contains("oembed", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = "{\"title\":\"Example Short\",\"author_name\":\"Example Author\",\"author_url\":\"https://youtube.com/channel/UC1234567890\"}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                }

                if (url.Contains("feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>", Encoding.UTF8, "application/xml")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok", Encoding.UTF8, "text/plain")
                };
            })));

            var queue = new BackgroundJobQueue();
            var transcriptService = new FakeTranscriptExtractionService(transcriptDir);
            var scopeFactory = new TestServiceScopeFactory(db, transcriptService);
            var service = new SourcesService(db, scraper, scopeFactory, queue);

            var result = await service.AddUrlSourceAsync(
                userId,
                draft.Id,
                "https://www.youtube.com/shorts/1olibnzyj4k",
                null,
                null,
                CancellationToken.None);

            var queuedJob = await queue.ReadAsync(CancellationToken.None);
            await queuedJob.Work(CancellationToken.None);

            var source = await db.Sources.FirstAsync(s => s.Id == result.SourceId);
            Assert.Equal(SourceKind.YouTube, source.Kind);
            Assert.Equal(TranscriptStatus.Complete, source.TranscriptStatus);
            Assert.Equal("1olibnzyj4k", source.YoutubeVideoId);
            Assert.Equal("Full transcript body", source.Content);
            Assert.Equal($"{source.Id}.json", source.TranscriptPath);
            Assert.Equal("https://www.youtube.com/shorts/1olibnzyj4k", source.Reference);
        }
        finally
        {
            if (Directory.Exists(transcriptDir))
            {
                Directory.Delete(transcriptDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SearchSourcesAsync_Filters_By_Kind()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft" };
        db.Drafts.Add(draft);
        var urlSource = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://example.com/url", Title = "URL Source" };
        var ytSource = new Source { Id = Guid.NewGuid(), Kind = SourceKind.YouTube, Reference = "https://youtube.com/watch?v=abc", Title = "YT Source" };
        db.Sources.AddRange(urlSource, ytSource);
        db.DraftSources.AddRange(
            new DraftSource { DraftId = draft.Id, SourceId = urlSource.Id },
            new DraftSource { DraftId = draft.Id, SourceId = ytSource.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);
        var result = await service.SearchSourcesAsync(userId, "", 1, 20, CancellationToken.None, kindFilter: SourceKind.YouTube);

        Assert.Single(result.Items);
        Assert.Equal("YouTube", result.Items[0].Kind);
    }

    [Fact]
    public async Task SearchSourcesAsync_Filters_By_DateRange()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft" };
        db.Drafts.Add(draft);
        var oldSource = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://old.com", Title = "Old", AddedAt = new DateTime(2025, 1, 1) };
        var newSource = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://new.com", Title = "New", AddedAt = new DateTime(2026, 7, 1) };
        db.Sources.AddRange(oldSource, newSource);
        db.DraftSources.AddRange(
            new DraftSource { DraftId = draft.Id, SourceId = oldSource.Id },
            new DraftSource { DraftId = draft.Id, SourceId = newSource.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);
        var result = await service.SearchSourcesAsync(userId, "", 1, 20, CancellationToken.None,
            addedAfter: new DateTime(2026, 6, 1));

        Assert.Single(result.Items);
        Assert.Equal("New", result.Items[0].Title);
    }

    [Fact]
    public async Task SearchSourcesAsync_ExcludesDraftId()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft1 = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft 1" };
        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft 2" };
        db.Drafts.AddRange(draft1, draft2);
        var s1 = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://a.com", Title = "Linked to draft1" };
        var s2 = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://b.com", Title = "Linked to draft2 only" };
        db.Sources.AddRange(s1, s2);
        db.DraftSources.AddRange(
            new DraftSource { DraftId = draft1.Id, SourceId = s1.Id },
            new DraftSource { DraftId = draft2.Id, SourceId = s2.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);
        var result = await service.SearchSourcesAsync(userId, "", 1, 20, CancellationToken.None, excludeDraftId: draft1.Id);

        Assert.Single(result.Items);
        Assert.Equal(s2.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetSourceDetailByIdAsync_Returns_Source_Without_DraftId()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = userId, Title = "Draft" };
        db.Drafts.Add(draft);
        var source = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://example.com", Title = "My Source", Content = "The content" };
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);
        var detail = await service.GetSourceDetailByIdAsync(userId, source.Id, CancellationToken.None);

        Assert.Equal(source.Id, detail.Id);
        Assert.Equal("My Source", detail.Title);
        Assert.Equal("The content", detail.Content);
        Assert.Equal(draft.Id, detail.DraftId);
    }

    [Fact]
    public async Task GetSourceDetailByIdAsync_Throws_When_Not_Accessible()
    {
        using var db = new AppDbContext(Options);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        db.Users.AddRange(
            new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" },
            new AppUser { Id = otherUserId, Username = "other", Email = "other@example.com", PasswordHash = "hash" });
        var draft = new Draft { Id = Guid.NewGuid(), UserId = otherUserId, Title = "Other draft" };
        db.Drafts.Add(draft);
        var source = new Source { Id = Guid.NewGuid(), Kind = SourceKind.Url, Reference = "https://example.com", Title = "Private Source" };
        db.Sources.Add(source);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetSourceDetailByIdAsync(userId, source.Id, CancellationToken.None));
    }
}

internal sealed class StaticHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StaticHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responder(request));
    }
}

internal sealed class FakeTranscriptExtractionService : ITranscriptExtractionService
{
    private readonly string _transcriptDir;

    public FakeTranscriptExtractionService(string transcriptDir)
    {
        _transcriptDir = transcriptDir;
    }

    public Task<bool> HealthAsync(CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    public async Task<TranscriptExtractionResult> ExtractAsync(string videoUrl, string outputPath, CancellationToken ct)
    {
        var fullPath = Path.Combine(_transcriptDir, outputPath);
        await File.WriteAllTextAsync(
            fullPath,
            "{\"videoUrl\":\"https://www.youtube.com/watch?v=abc123xyz09\",\"language\":\"en\",\"duration\":123,\"transcript\":\"Full transcript body\"}",
            ct);
        return new TranscriptExtractionResult(true, outputPath, 123, "en", null);
    }

    public async Task<TranscriptDocument?> ReadTranscriptAsync(string transcriptPath, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(_transcriptDir, transcriptPath), ct);
        return System.Text.Json.JsonSerializer.Deserialize<TranscriptDocument>(
            json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }
}

internal sealed class TestServiceScopeFactory : IServiceScopeFactory
{
    private readonly AppDbContext _db;
    private readonly ITranscriptExtractionService _transcriptService;

    public TestServiceScopeFactory(AppDbContext db, ITranscriptExtractionService transcriptService)
    {
        _db = db;
        _transcriptService = transcriptService;
    }

    public IServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(_transcriptService);
        var provider = services.BuildServiceProvider();
        return new TestServiceScope(provider);
    }

    private sealed class TestServiceScope : IServiceScope
    {
        public TestServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }
}
