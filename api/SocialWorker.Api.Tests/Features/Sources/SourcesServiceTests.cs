using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources;
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
            DraftId = draft.Id,
            Kind = SourceKind.Url,
            Reference = "https://example.com/source",
            Title = "Source Title",
            Content = "Cached body text here"
        };
        db.Drafts.Add(draft);
        db.Sources.Add(source);
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
            DraftId = draft.Id,
            Kind = SourceKind.Url,
            Reference = "https://example.com/source",
            Title = "Source Title"
        };
        db.Drafts.Add(draft);
        db.Sources.Add(source);
        await db.SaveChangesAsync();

        var service = new SourcesService(db, null!, null!, null!);

        await service.DeleteSourceAsync(userId, draft.Id, source.Id, CancellationToken.None);

        var deleted = await db.Sources.FirstOrDefaultAsync(s => s.Id == source.Id);
        Assert.Null(deleted);
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
