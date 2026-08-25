using System;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ListTemplatesToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_ReturnsTemplatesWithSlots()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Quote Template",
            Description = "A clean quote template",
            Width = 1080,
            Height = 1080,
            Schema = """
            {
                "name": "Quote Template",
                "slots": [
                    { "name": "quote_text", "type": "text", "required": true, "description": "The quote body" },
                    { "name": "author", "type": "text", "required": false }
                ],
                "layers": []
            }
            """,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var tool = new ListTemplatesTool(db);
        var result = await tool.ExecuteAsync(new ListTemplatesArgs(), ToolExecutionContext.Create(user.Id, null));

        Assert.Single(result.Items);
        Assert.Equal("Quote Template", result.Items[0].Name);
        Assert.Equal(2, result.Items[0].Slots.Count);
        Assert.Equal("quote_text", result.Items[0].Slots[0].Name);
        Assert.True(result.Items[0].Slots[0].Required);
        Assert.Contains("Quote Template", result.ToDisplayText());
    }

    [Fact]
    public async Task ExecuteAsync_FiltersBySearchKeyword()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        db.Templates.AddRange(
            new Template { Id = Guid.NewGuid(), Name = "Instagram Story", Width = 1080, Height = 1920, Schema = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Name = "Twitter Banner", Width = 1500, Height = 500, Schema = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var tool = new ListTemplatesTool(db);
        var result = await tool.ExecuteAsync(new ListTemplatesArgs(Search: "story"), ToolExecutionContext.Create(user.Id, null));

        Assert.Single(result.Items);
        Assert.Equal("Instagram Story", result.Items[0].Name);
    }
}
