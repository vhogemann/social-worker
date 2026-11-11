using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Charts.Models;
using SocialWorker.Api.Features.Charts.Services;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Charts;

public sealed class ChartServiceTests : SqliteTestBase
{
    [Fact]
    public async Task RenderAndAttachChartAsync_ValidRequest_RendersAndStoresMediaAsset()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "user", Email = "user@test.com", PasswordHash = "hash" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Draft", Content = "Content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var imageEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var chartService = new ChartService(imageEngine, mediaService, NullLogger<ChartService>.Instance);

        var request = new ChartRenderRequest(
            "bar",
            new List<string> { "Q1", "Q2", "Q3" },
            new List<ChartDataset> { new("Revenue", new List<double> { 10.5, 20.2, 35.0 }) },
            Title: "Quarterly Revenue",
            Subtitle: "Values in Millions USD",
            Theme: "dark",
            AspectRatio: "16:9",
            CardStyle: true);

        var result = await chartService.RenderAndAttachChartAsync(user.Id, draft.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.MediaAssetId);
        Assert.Contains($"media://{result.MediaAssetId}", result.MarkdownTag);
        Assert.Contains("Quarterly Revenue", result.MarkdownTag);

        var savedAsset = await db.MediaAssets.FindAsync(result.MediaAssetId.Value);
        Assert.NotNull(savedAsset);
        Assert.Equal("image/png", savedAsset.MimeType);
        Assert.Equal(draft.Id, savedAsset.DraftId);
        Assert.Contains("Chart: Quarterly Revenue - Values in Millions USD", savedAsset.AltText);
    }

    [Fact]
    public async Task RenderAndAttachChartAsync_MissingLabels_ReturnsFailure()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var imageEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var chartService = new ChartService(imageEngine, mediaService, NullLogger<ChartService>.Instance);

        var request = new ChartRenderRequest(
            "bar",
            new List<string>(),
            new List<ChartDataset> { new("Revenue", new List<double> { 10.0 }) });

        var result = await chartService.RenderAndAttachChartAsync(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("labels cannot be empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAndAttachChartAsync_MissingDatasets_ReturnsFailure()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var imageEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var chartService = new ChartService(imageEngine, mediaService, NullLogger<ChartService>.Instance);

        var request = new ChartRenderRequest(
            "bar",
            new List<string> { "Jan", "Feb" },
            new List<ChartDataset>());

        var result = await chartService.RenderAndAttachChartAsync(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("datasets cannot be empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderAndAttachChartAsync_EngineThrows_ReturnsSafeErrorResult()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var imageEngine = new FakeImageEngineClient
        {
            RenderChartHandler = _ => throw new InvalidOperationException("Image engine service unavailable")
        };
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var chartService = new ChartService(imageEngine, mediaService, NullLogger<ChartService>.Instance);

        var request = new ChartRenderRequest(
            "line",
            new List<string> { "Jan", "Feb" },
            new List<ChartDataset> { new("Users", new List<double> { 100, 200 }) });

        var result = await chartService.RenderAndAttachChartAsync(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Image engine service unavailable", result.Message);
    }
}
