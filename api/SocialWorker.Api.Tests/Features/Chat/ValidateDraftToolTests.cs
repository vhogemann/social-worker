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
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;
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

    private static IDraftValidatorResolver CreateResolver()
    {
        var blueskyValidator = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            new IValidateDraftRule<BlueskySegmentValidation>[]
            {
                new BlueskyMaxCharactersRule(),
                new BlueskyMaxImagesRule(),
                new BlueskyNoMixedImagesAndYouTubeRule(),
                new BlueskyUnsupportedMarkdownRule(),
                new BlueskyPlaceholderLinkRule(),
                new BlueskyPlaceholderMediaRule(),
                new BlueskyPlaceholderUrlRule(),
                new BlueskyMissingAltTextRule(),
                new BlueskyYouTubeEmbedSyntaxRule(),
                new BlueskyTitleLikeOpenerRule()
            });

        return new DraftValidatorResolver(
            new CanonicalDraftValidator(),
            blueskyValidator,
            new SocialWorker.Api.Features.Publishing.LinkedIn.LinkedInDraftValidator(),
            new PlatformContentPolicy());
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Validation_Report_With_Errors_And_Warnings_For_Bluesky()
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
            TargetPlatform = SocialPlatform.Bluesky,
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

        var tool = new ValidateDraftTool(db, CreateResolver());
        var report = await tool.ExecuteAsync(new ValidateDraftArgs(null), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draft.Id));
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
    public async Task ExecuteAsync_CanonicalDraft_Allows_LongContent_And_RichMarkdown()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Canonical Draft",
            TargetPlatform = null, // Canonical
            Content = $"# Header 1\n\nThis is a long-form canonical post that exceeds 300 characters with **bold formatting** and *italics* and lists.\n\n- Item 1\n- Item 2\n\n{new string('A', 500)}"
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var tool = new ValidateDraftTool(db, CreateResolver());
        var report = await tool.ExecuteAsync(new ValidateDraftArgs(null), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draft.Id));

        Assert.Equal(ValidateDraftOverallStatus.Valid, report.OverallStatus);
        Assert.False(report.HasBlockingErrors);
        Assert.Single(report.Posts);
        Assert.Empty(report.Posts[0].Issues);
    }

    [Fact]
    public async Task ExecuteAsync_Validates_Explicit_Content_Successfully()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tool = new ValidateDraftTool(db, CreateResolver());
        var explicitContent = "This is segment 1.\n---\nThis is segment 2.";
        
        var report = await tool.ExecuteAsync(new ValidateDraftArgs(explicitContent), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, null));
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

    [Fact]
    public async Task ExecuteAsync_LinkedIn_Explicit_Platform_Enforces_LinkedIn_Rules()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var tool = new ValidateDraftTool(db, CreateResolver());
        var explicitContent = "Segment 1\n---\nSegment 2";

        var report = await tool.ExecuteAsync(new ValidateDraftArgs(explicitContent, "LinkedIn"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, null));

        Assert.Equal(ValidateDraftOverallStatus.Failed, report.OverallStatus);
        Assert.True(report.HasBlockingErrors);
        Assert.Contains(report.Posts[0].Issues, i => i.Message.Contains("multi-post threads"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_For_NonEmbed_YouTube_Markdown_Link_On_Bluesky()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var tool = new ValidateDraftTool(db, CreateResolver());
        var explicitContent = "[Video Summary Source](https://www.youtube.com/shorts/xL3LA-Ftg3c)";

        var report = await tool.ExecuteAsync(new ValidateDraftArgs(explicitContent, "Bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, null));
        var display = report.ToDisplayText();

        Assert.Equal(ValidateDraftOverallStatus.Failed, report.OverallStatus);
        Assert.True(report.HasBlockingErrors);
        Assert.Contains("YouTube links must use embed syntax", display);
    }

    [Fact]
    public async Task ExecuteAsync_Allows_YouTube_Embed_Syntax_On_Bluesky()
    {
        using var db = new AppDbContext(_options);
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Username = "test", Email = "test@example.com", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var tool = new ValidateDraftTool(db, CreateResolver());
        var explicitContent = "![Video Summary Source](https://www.youtube.com/shorts/xL3LA-Ftg3c)";

        var report = await tool.ExecuteAsync(new ValidateDraftArgs(explicitContent, "Bluesky"), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, null));

        Assert.False(report.HasBlockingErrors);
        Assert.DoesNotContain(
            report.Posts.SelectMany(p => p.Issues),
            issue => issue.Message.Contains("YouTube links must use embed syntax", StringComparison.Ordinal));
    }
}
