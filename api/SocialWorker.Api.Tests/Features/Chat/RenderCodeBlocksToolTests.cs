using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class RenderCodeBlocksToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNoDraftId()
    {
        var tool = new RenderCodeBlocksTool(null!, null!, null!);

        var result = await tool.ExecuteAsync(new RenderCodeBlocksArgs(null, null), ToolExecutionContext.Create(Guid.NewGuid(), null));

        Assert.StartsWith("Error:", result);
        Assert.Contains("No active draft", result);
    }
}

public sealed class GeneratePlatformVariantsToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenInvalidDraftId()
    {
        var providerSvc = new LlmProviderService();
        var policy = new PlatformContentPolicy();
        var tool = new GeneratePlatformVariantsTool(null!, null!, null!, providerSvc, policy);

        var result = await tool.ExecuteAsync(
            new GeneratePlatformVariantsArgs("not-a-guid", new() { "Bluesky" }),
            ToolExecutionContext.Create(Guid.NewGuid(), Guid.NewGuid()));

        Assert.StartsWith("Error:", result);
        Assert.Contains("Invalid canonical", result);
    }
}