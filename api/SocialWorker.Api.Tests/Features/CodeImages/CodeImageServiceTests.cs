using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.Media;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class CodeImageServiceTests : SqliteTestBase
{
    [Fact]
    public async Task RenderAndStoreAsync_StoresPngMediaAsset()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "T", Content = "C", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var resizer = new ImageResizer();
        var storage = new FileStorageProvider();
        var media = new MediaService(db, resizer, storage);
        var renderer = new CodeImageRenderer();
        var svc = new CodeImageService(media, renderer);

        var block = new CodeBlock("csharp", "var x = 1;");
        var result = await svc.RenderAndStoreAsync(user.Id, draft.Id, block, CodeTheme.Dark, CancellationToken.None);

        Assert.Contains("media://", result.MarkdownTag);
        Assert.NotEqual(Guid.Empty, result.Id);
    }
}