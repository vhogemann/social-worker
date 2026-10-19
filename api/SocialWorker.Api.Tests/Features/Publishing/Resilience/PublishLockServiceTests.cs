using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Publishing.Resilience;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Resilience;

public sealed class PublishLockServiceTests
{
    [Fact]
    public async Task TryAcquireLockAsync_SingleCaller_AcquiresSuccessfully()
    {
        var lockService = new PublishLockService();
        var threadId = Guid.NewGuid();

        using var handle = await lockService.TryAcquireLockAsync(threadId, TimeSpan.FromSeconds(1));

        Assert.NotNull(handle);
    }

    [Fact]
    public async Task TryAcquireLockAsync_ConcurrentCallerOnSameThread_FailsIfTimeoutZero()
    {
        var lockService = new PublishLockService();
        var threadId = Guid.NewGuid();

        using var handle1 = await lockService.TryAcquireLockAsync(threadId, TimeSpan.FromSeconds(1));
        Assert.NotNull(handle1);

        using var handle2 = await lockService.TryAcquireLockAsync(threadId, TimeSpan.Zero);
        Assert.Null(handle2);
    }

    [Fact]
    public async Task TryAcquireLockAsync_DifferentThreads_DoNotBlockEachOther()
    {
        var lockService = new PublishLockService();
        var threadId1 = Guid.NewGuid();
        var threadId2 = Guid.NewGuid();

        using var handle1 = await lockService.TryAcquireLockAsync(threadId1, TimeSpan.FromSeconds(1));
        using var handle2 = await lockService.TryAcquireLockAsync(threadId2, TimeSpan.FromSeconds(1));

        Assert.NotNull(handle1);
        Assert.NotNull(handle2);
    }

    [Fact]
    public async Task TryAcquireLockAsync_AfterDispose_CanBeAcquiredAgain()
    {
        var lockService = new PublishLockService();
        var threadId = Guid.NewGuid();

        var handle1 = await lockService.TryAcquireLockAsync(threadId, TimeSpan.FromSeconds(1));
        Assert.NotNull(handle1);
        handle1!.Dispose();

        using var handle2 = await lockService.TryAcquireLockAsync(threadId, TimeSpan.FromSeconds(1));
        Assert.NotNull(handle2);
    }
}
