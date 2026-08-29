using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.BackgroundTasks.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.BackgroundTasks;

public sealed class PostgreSqlBackgroundTaskQueueIntegrationTests : PostgreSqlIntegrationTestBase
{
    private sealed record TestPayload(string Message, int Number);

    private PostgresBackgroundTaskQueue CreateQueue()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new PostgresBackgroundTaskQueue(scopeFactory, NullLogger<PostgresBackgroundTaskQueue>.Instance);
    }

    [Fact]
    public async Task Postgres_EnqueueAndDequeue_ExecutesSkipLockedQuerySuccessfully()
    {
        var queue = CreateQueue();

        var payload = new TestPayload("Postgres Real Test", 999);
        var taskId = await queue.EnqueueAsync("pg-test-job", payload);

        // Dequeue should execute the PostgreSQL FOR UPDATE SKIP LOCKED query
        var claimed = await queue.DequeueNextAsync("pg-worker-1", TimeSpan.FromMinutes(5));

        Assert.NotNull(claimed);
        Assert.Equal(taskId, claimed!.Id);
        Assert.Equal(BackgroundTaskStatus.Running, claimed.Status);
        Assert.Equal("pg-worker-1", claimed.LockedByWorker);
        Assert.Equal(1, claimed.AttemptCount);
        Assert.NotNull(claimed.LockedUntil);

        // Complete the task
        await queue.MarkCompletedAsync(taskId);

        using var verifyDb = CreateDbContext();
        var stored = await verifyDb.BackgroundTasks.FindAsync(taskId);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Completed, stored!.Status);
        Assert.NotNull(stored.CompletedAt);
    }

    [Fact]
    public async Task Postgres_ConcurrentWorkers_ClaimDistinctTasksWithoutCollisions()
    {
        var queue = CreateQueue();

        // Enqueue 5 tasks
        var taskIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var id = await queue.EnqueueAsync("pg-concurrent-job", new TestPayload($"Item {i}", i));
            taskIds.Add(id);
        }

        // Dequeue concurrently from 5 distinct workers
        var dequeueTasks = Enumerable.Range(1, 5).Select(i =>
            queue.DequeueNextAsync($"concurrent-worker-{i}", TimeSpan.FromMinutes(5))
        ).ToList();

        var results = await Task.WhenAll(dequeueTasks);

        var claimedIds = results.Where(r => r != null).Select(r => r!.Id).ToList();

        // All 5 tasks should be claimed uniquely
        Assert.Equal(5, claimedIds.Count);
        Assert.Equal(5, claimedIds.Distinct().Count());
        Assert.All(taskIds, id => Assert.Contains(id, claimedIds));
    }

    [Fact]
    public async Task Postgres_ExpiredLease_IsReclaimedByAnotherWorker()
    {
        var queue = CreateQueue();

        using (var db = CreateDbContext())
        {
            var expiredTask = BackgroundTask.Create("pg-stale-job", new TestPayload("Dead worker job", 1));
            expiredTask.Status = BackgroundTaskStatus.Running;
            expiredTask.LockedByWorker = "crashed-node";
            expiredTask.LockedUntil = DateTime.UtcNow.AddMinutes(-10); // 10 minutes in the past
            db.BackgroundTasks.Add(expiredTask);
            await db.SaveChangesAsync();

            var reclaimed = await queue.DequeueNextAsync("active-worker", TimeSpan.FromMinutes(5));
            Assert.NotNull(reclaimed);
            Assert.Equal(expiredTask.Id, reclaimed!.Id);
            Assert.Equal("active-worker", reclaimed.LockedByWorker);
        }
    }
}
