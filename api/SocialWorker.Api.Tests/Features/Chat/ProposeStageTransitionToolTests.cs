using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ProposeStageTransitionToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess()
    {
        var tool = new ProposeStageTransitionTool();
        var args = new ProposeStageTransitionArgs("Bluesky", "Ready", "Ready for review");
        var result = await tool.ExecuteAsync(args, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal("Bluesky", result.Platform);
        Assert.Equal("Ready", result.ProposedStage);
        Assert.Equal("Ready for review", result.Reasoning);
    }
}