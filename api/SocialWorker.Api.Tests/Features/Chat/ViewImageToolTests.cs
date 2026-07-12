using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class ViewImageToolTests
{
    [Fact]
    public async Task ExecuteAsync_Throws_ForInvalidId()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var tool = new ViewImageTool(sp.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tool.ExecuteAsync(new ViewImageArgs("bad-id"), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }
}