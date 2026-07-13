using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class RenderCodeBlocksToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNoDraftId()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var tool = new RenderCodeBlocksTool(sp.GetRequiredService<IServiceScopeFactory>());

        var result = await tool.ExecuteAsync(new RenderCodeBlocksArgs(null, null), null, Guid.NewGuid(), CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("No active draft", result);
    }
}

public sealed class GeneratePlatformVariantsToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenInvalidDraftId()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var providerSvc = new LlmProviderService();
        var policy = new PlatformContentPolicy();
        var tool = new GeneratePlatformVariantsTool(sp.GetRequiredService<IServiceScopeFactory>(), providerSvc, policy);

        var result = await tool.ExecuteAsync(
            new GeneratePlatformVariantsArgs("not-a-guid", new() { "Bluesky" }),
            Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("Invalid canonical", result);
    }
}