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

public sealed class FetchSourceToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSourceContent()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "u", Email = "u@e.com", PasswordHash = "h" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "T", Content = "C", UserId = user.Id };
        db.Drafts.Add(draft);
        var source = new Source { Id = Guid.NewGuid(), DraftId = draft.Id, Kind = SourceKind.Url, Reference = "https://example.com", Title = "Ex", Content = "Source content here" };
        db.Sources.Add(source);
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var tool = new FetchSourceTool(scopeFactory);

        var result = await tool.ExecuteAsync(new FetchSourceArgs(source.Id.ToString()), draft.Id, user.Id, CancellationToken.None);

        Assert.Equal(source.Id, result.Id);
        Assert.Equal("Source content here", result.Content);
    }
}