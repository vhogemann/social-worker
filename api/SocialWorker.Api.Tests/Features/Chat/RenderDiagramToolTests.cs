using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Diagrams.Models;
using SocialWorker.Api.Features.Diagrams.Services;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class RenderDiagramToolTests : SqliteTestBase
{
    [Fact]
    public void ToolMetadata_HasCorrectNameAndParameters()
    {
        var tool = new RenderDiagramTool(null!);

        Assert.Equal("render_diagram", tool.Name);
        Assert.Contains("Mermaid", tool.Description);
        Assert.True(tool.Parameters.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("code", out _));
        Assert.True(props.TryGetProperty("theme", out _));
        Assert.True(props.TryGetProperty("aspect_ratio", out _));
        Assert.True(props.TryGetProperty("card_style", out _));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDraftId_ReturnsError()
    {
        var diagramService = new DiagramService(new FakeImageEngineClient(), null!, NullLogger<DiagramService>.Instance);
        var tool = new RenderDiagramTool(diagramService);

        var args = new RenderDiagramArgs("flowchart LR\n  A --> B");
        var context = ToolExecutionContext.Create(Guid.NewGuid(), null);

        var result = await tool.ExecuteAsync(args, context);

        Assert.False(result.Success);
        Assert.Contains("active draft is required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidArgs_RendersAndAttachesDiagram()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "user", Email = "u@test.com", PasswordHash = "p" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Draft", Content = "Content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        DiagramRenderRequest? captured = null;
        var imageEngine = new FakeImageEngineClient
        {
            RenderDiagramHandler = req =>
            {
                captured = req;
                return Task.FromResult(new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 });
            }
        };
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var diagramService = new DiagramService(imageEngine, mediaService, NullLogger<DiagramService>.Instance);
        var tool = new RenderDiagramTool(diagramService);

        var args = new RenderDiagramArgs(
            "sequenceDiagram\n  User->>API: Render",
            Title: "API Sequence",
            Subtitle: "Async Flow");

        var context = ToolExecutionContext.Create(user.Id, draft.Id);
        var result = await tool.ExecuteAsync(args, context);

        Assert.True(result.Success);
        Assert.NotNull(result.MediaAssetId);
        Assert.Contains($"media://{result.MediaAssetId}", result.MarkdownTag);
        Assert.Contains("API Sequence", result.MarkdownTag);
        Assert.NotNull(captured);
        Assert.Equal("dark", captured!.Theme);
        Assert.Equal("auto", captured.AspectRatio);
        Assert.True(captured.CardStyle);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCode_ReturnsValidationError()
    {
        var diagramService = new DiagramService(new FakeImageEngineClient(), null!, NullLogger<DiagramService>.Instance);
        var tool = new RenderDiagramTool(diagramService);

        var context = ToolExecutionContext.Create(Guid.NewGuid(), Guid.NewGuid());
        var result = await tool.ExecuteAsync(new RenderDiagramArgs(""), context);

        Assert.False(result.Success);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
