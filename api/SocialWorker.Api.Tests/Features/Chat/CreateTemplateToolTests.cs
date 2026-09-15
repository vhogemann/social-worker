using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class CreateTemplateToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_CreatesTemplateAndSavesToDbWithUserId()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var fakeEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, fakeEngine, storage);

        var tool = new CreateTemplateTool(db, fakeEngine, mediaService);
        var schema = """
        {
          "name": "Minimal Card",
          "slots": [{ "name": "title", "type": "text" }],
          "layers": [{ "type": "rect", "fill": "#000000", "x": 0, "y": 0, "w": "100%", "h": "100%" }]
        }
        """;

        var args = new CreateTemplateArgs("Minimal Card", schema, "Clean dark card", 1080, 1080);
        var context = ToolExecutionContext.Create(user.Id, null);

        var result = await tool.ExecuteAsync(args, context);

        Assert.True(result.Success);
        Assert.NotNull(result.TemplateId);
        Assert.Contains("Minimal Card", result.Message);

        var saved = await db.Templates.FirstOrDefaultAsync(t => t.Id == result.TemplateId.Value);
        Assert.NotNull(saved);
        Assert.Equal("Minimal Card", saved.Name);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(1080, saved.Width);
        Assert.Equal(1080, saved.Height);
    }

    [Fact]
    public async Task ExecuteAsync_AttachesPreviewWhenDraftIsActive()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Test Draft",
            Content = "Initial content"
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var fakeEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, fakeEngine, storage);

        var tool = new CreateTemplateTool(db, fakeEngine, mediaService);
        var schema = "{\"name\":\"Test\",\"slots\":[],\"layers\":[]}";
        var args = new CreateTemplateArgs("Test Card", schema);
        var context = ToolExecutionContext.Create(user.Id, draft.Id);

        var result = await tool.ExecuteAsync(args, context);

        Assert.True(result.Success);
        Assert.NotNull(result.MarkdownTag);
        Assert.StartsWith("![", result.MarkdownTag);

        var saved = await db.Templates.FirstOrDefaultAsync(t => t.Id == result.TemplateId!.Value);
        Assert.NotNull(saved);
        Assert.NotNull(saved.ThumbnailMediaAssetId);
        Assert.Equal(user.Id, saved.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorWhenNameOrSchemaIsMissing()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var fakeEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, fakeEngine, storage);

        var tool = new CreateTemplateTool(db, fakeEngine, mediaService);
        var context = ToolExecutionContext.Create(user.Id, null);

        var emptyNameResult = await tool.ExecuteAsync(new CreateTemplateArgs("", "{}"), context);
        Assert.False(emptyNameResult.Success);

        var emptySchemaResult = await tool.ExecuteAsync(new CreateTemplateArgs("Name", ""), context);
        Assert.False(emptySchemaResult.Success);
    }
}
