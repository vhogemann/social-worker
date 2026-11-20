using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Diagrams.Models;
using SocialWorker.Api.Features.Diagrams.Services;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Diagrams;

public sealed class DiagramServiceTests : SqliteTestBase
{
    [Fact]
    public async Task RenderAndAttachDiagramAsync_ValidRequest_RendersAndStoresMediaAsset()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "user", Email = "user@test.com", PasswordHash = "hash" };
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

        var request = new DiagramRenderRequest(
            "```mermaid\nflowchart LR\n  A[Client] --> B[API]\n```",
            Title: "Request Pipeline",
            Subtitle: "End-to-end flow",
            Theme: "tokyo-night",
            AspectRatio: "auto",
            CardStyle: true);

        var result = await diagramService.RenderAndAttachDiagramAsync(user.Id, draft.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.MediaAssetId);
        Assert.Contains($"media://{result.MediaAssetId}", result.MarkdownTag);
        Assert.Contains("Request Pipeline", result.MarkdownTag);
        Assert.NotNull(captured);
        Assert.Equal("flowchart LR\n  A[Client] --> B[API]", captured!.Code);
        Assert.Equal("tokyo-night", captured.Theme);
        Assert.Equal("auto", captured.AspectRatio);

        var savedAsset = await db.MediaAssets.FindAsync(result.MediaAssetId.Value);
        Assert.NotNull(savedAsset);
        Assert.Equal("image/png", savedAsset.MimeType);
        Assert.Equal(draft.Id, savedAsset.DraftId);
        Assert.Equal("Diagram: Request Pipeline - End-to-end flow", savedAsset.AltText);
    }

    [Fact]
    public async Task RenderAndAttachDiagramAsync_EmptyCode_ReturnsFailure()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var imageEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var diagramService = new DiagramService(imageEngine, mediaService, NullLogger<DiagramService>.Instance);

        var result = await diagramService.RenderAndAttachDiagramAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DiagramRenderRequest("   "),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAndAttachDiagramAsync_SyntaxErrorFromEngine_ReturnsActionableError()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var imageEngine = new FakeImageEngineClient
        {
            RenderDiagramHandler = _ => throw new ArgumentException("Invalid Mermaid diagram syntax: Unexpected token. Supported Mermaid diagram types: flowchart")
        };
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var diagramService = new DiagramService(imageEngine, mediaService, NullLogger<DiagramService>.Instance);

        var result = await diagramService.RenderAndAttachDiagramAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DiagramRenderRequest("gantt\n  title Bad"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Invalid Mermaid diagram syntax", result.Message);
        Assert.Contains("flowchart", result.Message);
    }
}
