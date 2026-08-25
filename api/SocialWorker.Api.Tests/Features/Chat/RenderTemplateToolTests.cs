using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class RenderTemplateToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_RendersAndAttachesImage()
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

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Quote Card",
            Width = 1080,
            Height = 1080,
            Schema = "{\"name\":\"Quote Card\",\"layers\":[]}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var fakeEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, fakeEngine, storage);
        var tool = new RenderTemplateTool(db, fakeEngine, mediaService);

        var args = new RenderTemplateArgs(
            TemplateId: template.Id.ToString(),
            Slots: new Dictionary<string, string> { { "quote", "Hello world" } },
            AltText: "A generated quote card");

        var result = await tool.ExecuteAsync(args, ToolExecutionContext.Create(user.Id, draft.Id));

        Assert.True(result.Success);
        Assert.NotNull(result.MarkdownTag);
        Assert.StartsWith("![", result.MarkdownTag);
        Assert.Contains("media://", result.MarkdownTag);

        var asset = await db.MediaAssets.FirstOrDefaultAsync(m => m.DraftId == draft.Id);
        Assert.NotNull(asset);
        Assert.Equal("A generated quote card", asset.AltText);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesSlotAliases()
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

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Quote Card",
            Width = 1080,
            Height = 1080,
            Schema = "{\"name\":\"Quote Card\",\"slots\":[{\"name\":\"quote_text\",\"type\":\"text\",\"required\":true}],\"layers\":[]}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var fakeEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, fakeEngine, storage);
        var tool = new RenderTemplateTool(db, fakeEngine, mediaService);

        var args = new RenderTemplateArgs(
            TemplateId: template.Id.ToString(),
            Slots: new Dictionary<string, string> { { "quote", "Hello world" } });

        var result = await tool.ExecuteAsync(args, ToolExecutionContext.Create(user.Id, draft.Id));

        Assert.True(result.Success);
        Assert.NotNull(result.MarkdownTag);
    }
}
