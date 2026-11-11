using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Charts.Models;
using SocialWorker.Api.Features.Charts.Services;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class RenderChartToolTests : SqliteTestBase
{
    [Fact]
    public void ToolMetadata_HasCorrectNameAndParameters()
    {
        var tool = new RenderChartTool(null!);

        Assert.Equal("render_chart", tool.Name);
        Assert.Contains("statistical or numerical", tool.Description);
        Assert.True(tool.Parameters.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("type", out _));
        Assert.True(props.TryGetProperty("labels", out _));
        Assert.True(props.TryGetProperty("datasets", out _));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDraftId_ReturnsError()
    {
        var chartService = new ChartService(new FakeImageEngineClient(), null!, NullLogger<ChartService>.Instance);
        var tool = new RenderChartTool(chartService);

        var args = new RenderChartArgs("bar", new[] { "A" }, new[] { new RenderChartDatasetArgs("L", new double[] { 1 }) });
        var context = ToolExecutionContext.Create(Guid.NewGuid(), null);

        var result = await tool.ExecuteAsync(args, context);

        Assert.False(result.Success);
        Assert.Contains("active draft is required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidArgs_RendersAndAttachesChart()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var user = new AppUser { Id = Guid.NewGuid(), Username = "user", Email = "u@test.com", PasswordHash = "p" };
        db.Users.Add(user);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Draft", Content = "Content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var imageEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);
        var chartService = new ChartService(imageEngine, mediaService, NullLogger<ChartService>.Instance);
        var tool = new RenderChartTool(chartService);

        var args = new RenderChartArgs(
            "bar",
            new[] { "Q1", "Q2", "Q3", "Q4" },
            new[] { new RenderChartDatasetArgs("Revenue", new double[] { 10, 20, 30, 40 }) },
            Title: "FY26 Revenue",
            Subtitle: "In Billions",
            Theme: "dark",
            AspectRatio: "16:9",
            CardStyle: true);

        var context = ToolExecutionContext.Create(user.Id, draft.Id);
        var result = await tool.ExecuteAsync(args, context);

        Assert.True(result.Success);
        Assert.NotNull(result.MediaAssetId);
        Assert.Contains($"media://{result.MediaAssetId}", result.MarkdownTag);
        Assert.Contains("FY26 Revenue", result.MarkdownTag);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredFields_ReturnsValidationError()
    {
        var chartService = new ChartService(new FakeImageEngineClient(), null!, NullLogger<ChartService>.Instance);
        var tool = new RenderChartTool(chartService);

        var context = ToolExecutionContext.Create(Guid.NewGuid(), Guid.NewGuid());
        var result = await tool.ExecuteAsync(new RenderChartArgs("", new List<string>(), new List<RenderChartDatasetArgs>()), context);

        Assert.False(result.Success);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
