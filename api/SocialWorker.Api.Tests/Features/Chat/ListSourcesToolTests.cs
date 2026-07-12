using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ListSourcesToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSourcesForDraft()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "T", Content = "C", UserId = user.Id };
        db.Drafts.Add(draft);
        db.Sources.Add(new Source { DraftId = draft.Id, Kind = SourceKind.Url, Reference = "https://example.com", Title = "Example" });
        db.Sources.Add(new Source { DraftId = draft.Id, Kind = SourceKind.Url, Reference = "https://test.com", Title = "Test" });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var tool = new ListSourcesTool(scopeFactory);

        var result = await tool.ExecuteAsync(new ListSourcesArgs(), draft.Id, user.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Reference == "https://example.com");
    }
}