using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Tests;

public sealed class SourcesServiceTests : SqliteTestBase
{
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

        var service = new SourcesService(db, null!, null!);

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

        var service = new SourcesService(db, null!, null!);

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

        var service = new SourcesService(db, null!, null!);

        await service.DeleteSourceAsync(userId, draft.Id, source.Id, CancellationToken.None);

        var deleted = await db.Sources.FirstOrDefaultAsync(s => s.Id == source.Id);
        Assert.Null(deleted);
    }
}
