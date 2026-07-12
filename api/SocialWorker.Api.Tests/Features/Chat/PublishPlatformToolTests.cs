using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class PublishPlatformToolTests : SqliteTestBase
{
    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNoDraftId()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var tool = new PublishPlatformTool(sp.GetRequiredService<IServiceScopeFactory>());

        var result = await tool.ExecuteAsync(new PublishPlatformArgs("Bluesky"), null, Guid.NewGuid(), CancellationToken.None);

        var error = result.GetType().GetProperty("error")?.GetValue(result) as string;
        Assert.Contains("No active draft", error);
    }
}