using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class MediaServiceTests : SqliteTestBase
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static async Task<(AppDbContext Db, MediaService Service, AppUser User, Draft Draft)> CreateAsync(AppDbContext db, string altText = null)
    {
        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "T", Content = "C", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var resizer = new ImageResizer();
        var storage = new FileStorageProvider();
        var svc = new MediaService(db, resizer, storage);
        return (db, svc, user, draft);
    }

    [Fact]
    public async Task UploadMediaAsync_Throws_For_Nonexistent_Draft()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, _, _) = await CreateAsync(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UploadMediaAsync(Guid.NewGuid(), Guid.NewGuid(), "test.png", "image/png",
                new MemoryStream(TinyPng), CancellationToken.None));
    }

    [Fact]
    public async Task UploadMediaAsync_Returns_MarkdownTag_On_Success()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, user, draft) = await CreateAsync(db);

        var result = await svc.UploadMediaAsync(user.Id, draft.Id, "test.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None);

        Assert.StartsWith("![test.png](media://", result.MarkdownTag);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task UploadMediaAsync_Deduplicates_Same_Content()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, user, draft) = await CreateAsync(db);

        var first = await svc.UploadMediaAsync(user.Id, draft.Id, "a.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None);
        var second = await svc.UploadMediaAsync(user.Id, draft.Id, "b.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None);

        var assets = await db.MediaAssets.Where(m => m.DraftId == draft.Id).ToListAsync();
        Assert.Equal(2, assets.Count);
        Assert.Equal(assets[0].FilePath, assets[1].FilePath);
    }

    [Fact]
    public async Task GetMediaFileAsync_Throws_For_Missing_Asset()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, _, _) = await CreateAsync(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetMediaFileAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetMediaFileAsync_Returns_Path_And_MimeType()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, user, draft) = await CreateAsync(db);

        var uploaded = await svc.UploadMediaAsync(user.Id, draft.Id, "test.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None);

        var (fullPath, mimeType) = await svc.GetMediaFileAsync(uploaded.Id, CancellationToken.None);

        Assert.NotNull(fullPath);
        Assert.Equal("image/png", mimeType);
    }

    [Fact]
    public async Task UpdateMediaAltTextAsync_Updates_Alt_Text()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, user, draft) = await CreateAsync(db);

        var uploaded = await svc.UploadMediaAsync(user.Id, draft.Id, "test.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None, altText: "original");

        var updated = await svc.UpdateMediaAltTextAsync(user.Id, uploaded.Id, "new alt text", CancellationToken.None);

        Assert.Equal("new alt text", updated.AltText);
    }

    [Fact]
    public async Task DeleteMediaAsync_Removes_Asset()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, user, draft) = await CreateAsync(db);

        var uploaded = await svc.UploadMediaAsync(user.Id, draft.Id, "test.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None);

        await svc.DeleteMediaAsync(user.Id, uploaded.Id, CancellationToken.None);

        Assert.Null(await db.MediaAssets.FindAsync(uploaded.Id));
    }

    [Fact]
    public async Task DeleteMediaAsync_Throws_For_Wrong_User()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var (_, svc, user, draft) = await CreateAsync(db);

        var uploaded = await svc.UploadMediaAsync(user.Id, draft.Id, "test.png", "image/png",
            new MemoryStream(TinyPng), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteMediaAsync(Guid.NewGuid(), uploaded.Id, CancellationToken.None));
    }
}