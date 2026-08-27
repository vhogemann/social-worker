using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Infrastructure.Background;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure.Background;

public sealed class BackgroundJobHostedServiceTests
{
    [Fact]
    public async Task WorkerPool_ProcessesConcurrentJobsWithoutDeadlock()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var queue = new BackgroundJobQueue(scopeFactory);
        var hostedService = new BackgroundJobHostedService(
            queue,
            NullLogger<BackgroundJobHostedService>.Instance,
            null);

        using var cts = new CancellationTokenSource();
        var serviceTask = hostedService.StartAsync(cts.Token);

        var parentStarted = new TaskCompletionSource<bool>();
        var childCompleted = new TaskCompletionSource<bool>();

        // Enqueue parent job that enqueues child job and waits for child to complete
        queue.Enqueue(new BackgroundJobQueue.Job("parent-job", async ct =>
        {
            parentStarted.SetResult(true);

            // Enqueue child job
            queue.Enqueue(new BackgroundJobQueue.Job("child-job", ctChild =>
            {
                childCompleted.SetResult(true);
                return Task.CompletedTask;
            }));

            // Wait for child job to complete (would deadlock with 1 worker)
            await childCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }));

        await parentStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await childCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(childCompleted.Task.IsCompletedSuccessfully);

        await hostedService.StopAsync(CancellationToken.None);
        cts.Cancel();
    }
}
