using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class ValidateDraftToolTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ValidateDraftToolTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Validation_Report_With_Errors_And_Warnings()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);

        var mediaId = Guid.NewGuid();
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Draft",
            Content = $"This is post 1. Valid.\n---\nThis is post 2 with an image: ![](media://{mediaId}) and a youtube link https://youtube.com/watch?v=123. It also has a lot of text repeating so that it easily goes over the 300 characters limit. Let's repeat: It also has a lot of text repeating so that it easily goes over the 300 characters limit. Let's repeat again: It also has a lot of text repeating so that it easily goes over the 300 characters limit."
        };
        var asset = new MediaAsset
        {
            Id = mediaId,
            DraftId = draft.Id,
            FileName = "image.png",
            FilePath = "image.png",
            MimeType = "image/png",
            AltText = ""
        };

        db.Drafts.Add(draft);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(db);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var tool = new ValidateDraftTool(scopeFactory, new BlueskyContentValidator());
        var report = await tool.ExecuteAsync(new ValidateDraftArgs(null), draft.Id, userId, CancellationToken.None);
        var display = report.ToDisplayText();

        Assert.Equal(ValidateDraftOverallStatus.Failed, report.OverallStatus);
        Assert.True(report.HasBlockingErrors);
        Assert.Equal(2, report.Posts.Count);
        Assert.Contains("### Draft Validation Report", display);
        Assert.Contains("Post 1:", display);
        Assert.Contains("**Status**: Valid", display);
        Assert.Contains("Post 2:", display);
        Assert.Contains("Exceeds the 300-character limit", display);
        Assert.Contains("Cannot mix images and YouTube embeds", display);
        Assert.Contains("Missing ALT text on images: image.png", display);
        Assert.Contains("Validation failed", display);
    }

    [Fact]
    public async Task ExecuteAsync_Validates_Explicit_Content_Successfully()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(db);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var tool = new ValidateDraftTool(scopeFactory, new BlueskyContentValidator());
        var explicitContent = "This is segment 1 under 300 chars.\n---\nThis is segment 2 under 300 chars.";
        
        var report = await tool.ExecuteAsync(new ValidateDraftArgs(explicitContent), null, userId, CancellationToken.None);
        var display = report.ToDisplayText();

        Assert.Equal(ValidateDraftOverallStatus.Valid, report.OverallStatus);
        Assert.False(report.HasBlockingErrors);
        Assert.Equal(2, report.Posts.Count);
        Assert.Contains("### Draft Validation Report", display);
        Assert.Contains("Post 1:", display);
        Assert.Contains("Post 2:", display);
        Assert.Contains("**Status**: Valid", display);
        Assert.Contains("Overall Status**: Valid", display);
    }
}
