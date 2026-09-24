using System;
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

public sealed class PostgresBackgroundTaskQueueTests : SqliteTestBase
{
    private sealed record SamplePayload(string Name, int Value);

    private PostgresBackgroundTaskQueue CreateQueue()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new PostgresBackgroundTaskQueue(scopeFactory, NullLogger<PostgresBackgroundTaskQueue>.Instance);
    }

    [Fact]
    public async Task EnqueueAsync_PersistsTaskInDatabase()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var payload = new SamplePayload("Test", 42);
        var taskId = await queue.EnqueueAsync("sample-task", payload);

        var stored = await db.BackgroundTasks.FindAsync(taskId);
        Assert.NotNull(stored);
        Assert.Equal("sample-task", stored!.TaskType);
        Assert.Equal(BackgroundTaskStatus.Pending, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Contains("\"Name\":\"Test\"", stored.PayloadJson);
    }

    [Fact]
    public async Task EnqueueAsync_WithDeduplicationKey_DoesNotDuplicatePendingTask()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var payload = new SamplePayload("Item", 1);
        var options = EnqueueTaskOptions.WithDeduplication("dedup-key-1");

        var id1 = await queue.EnqueueAsync("sample-task", payload, options);
        var id2 = await queue.EnqueueAsync("sample-task", payload, options);

        Assert.Equal(id1, id2);
        var totalCount = await db.BackgroundTasks.CountAsync(t => t.DeduplicationKey == "dedup-key-1");
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task DequeueNextAsync_ClaimsAndLocksPendingTask()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var taskId = await queue.EnqueueAsync("sample-task", new SamplePayload("Test", 1));

        var claimed = await queue.DequeueNextAsync("worker-1", TimeSpan.FromMinutes(5));
        Assert.NotNull(claimed);
        Assert.Equal(taskId, claimed!.Id);
        Assert.Equal(BackgroundTaskStatus.Running, claimed.Status);
        Assert.Equal("worker-1", claimed.LockedByWorker);
        Assert.Equal(1, claimed.AttemptCount);
        Assert.NotNull(claimed.LockedUntil);
        Assert.True(claimed.LockedUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task DequeueNextAsync_PrioritizesHigherPriorityTasks()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var lowId = await queue.EnqueueAsync("sample-task", new SamplePayload("Low", 1), EnqueueTaskOptions.WithPriority(0));
        var highId = await queue.EnqueueAsync("sample-task", new SamplePayload("High", 2), EnqueueTaskOptions.WithPriority(10));

        var first = await queue.DequeueNextAsync("worker-1", TimeSpan.FromMinutes(5));
        Assert.NotNull(first);
        Assert.Equal(highId, first!.Id);

        var second = await queue.DequeueNextAsync("worker-2", TimeSpan.FromMinutes(5));
        Assert.NotNull(second);
        Assert.Equal(lowId, second!.Id);
    }

    [Fact]
    public async Task MarkCompletedAsync_MarksTaskCompleted()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var taskId = await queue.EnqueueAsync("sample-task", new SamplePayload("Test", 1));
        await queue.DequeueNextAsync("worker-1", TimeSpan.FromMinutes(5));

        await queue.MarkCompletedAsync(taskId);

        var stored = await db.BackgroundTasks.FindAsync(taskId);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Completed, stored!.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.Null(stored.LockedByWorker);
        Assert.Null(stored.LockedUntil);
    }

    [Fact]
    public async Task MarkRetryAsync_ReschedulesTaskWithDelay()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var taskId = await queue.EnqueueAsync("sample-task", new SamplePayload("Test", 1));
        await queue.DequeueNextAsync("worker-1", TimeSpan.FromMinutes(5));

        await queue.MarkRetryAsync(taskId, "Transient network timeout", TimeSpan.FromMinutes(2));

        var stored = await db.BackgroundTasks.FindAsync(taskId);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Pending, stored!.Status);
        Assert.Equal("Transient network timeout", stored.LastError);
        Assert.True(stored.NextAttemptAt > DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task CrashRecovery_ExpiredLeaseCanBeReclaimedByAnotherWorker()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var task = BackgroundTask.Create("sample-task", new SamplePayload("Test", 1));
        task.Status = BackgroundTaskStatus.Running;
        task.LockedByWorker = "crashed-worker";
        task.LockedUntil = DateTime.UtcNow.AddMinutes(-5); // Expired 5 mins ago
        db.BackgroundTasks.Add(task);
        await db.SaveChangesAsync();

        var reclaimed = await queue.DequeueNextAsync("worker-healthy", TimeSpan.FromMinutes(5));
        Assert.NotNull(reclaimed);
        Assert.Equal(task.Id, reclaimed!.Id);
        Assert.Equal("worker-healthy", reclaimed.LockedByWorker);
        Assert.Equal(BackgroundTaskStatus.Running, reclaimed.Status);
    }

    [Fact]
    public async Task ExtendLeaseAsync_ExtendsLockTimeForCurrentWorker()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var taskId = await queue.EnqueueAsync("sample-task", new SamplePayload("Test", 1));
        var claimed = await queue.DequeueNextAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.NotNull(claimed);
        var originalLock = claimed!.LockedUntil!.Value;

        var success = await queue.ExtendLeaseAsync(taskId, "worker-1", TimeSpan.FromMinutes(5));
        Assert.True(success);

        var stored = await db.BackgroundTasks.FindAsync(taskId);
        Assert.NotNull(stored);
        Assert.True(stored!.LockedUntil > originalLock);
    }

    [Fact]
    public async Task RetryTaskAsync_ResetsFailedTaskToPending()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var task = BackgroundTask.Create("sample-task", new SamplePayload("Test", 1));
        task.MarkFailed("Fatal error");
        task.AttemptCount = 3;
        db.BackgroundTasks.Add(task);
        await db.SaveChangesAsync();

        var retried = await queue.RetryTaskAsync(task.Id);
        Assert.True(retried);

        using var verifyDb = CreateDbContext();
        var stored = await verifyDb.BackgroundTasks.FindAsync(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Pending, stored!.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Null(stored.LastError);
    }

    [Fact]
    public async Task DequeueNextAsync_WithAllowedTaskTypes_OnlyDequeuesMatchingTasks()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var ytTaskId = await queue.EnqueueAsync("source-transcription", new SamplePayload("YT", 1));
        var feedTaskId = await queue.EnqueueAsync("feed-ingestion", new SamplePayload("Feed", 2));

        // Worker allowing only source-transcription
        var ytClaimed = await queue.DequeueNextAsync("worker-yt", TimeSpan.FromMinutes(5), new[] { "source-transcription" });
        Assert.NotNull(ytClaimed);
        Assert.Equal(ytTaskId, ytClaimed!.Id);
        Assert.Equal("source-transcription", ytClaimed.TaskType);

        // Another query from worker-yt should return null (since only feed-ingestion remains)
        var ytSecondClaim = await queue.DequeueNextAsync("worker-yt", TimeSpan.FromMinutes(5), new[] { "source-transcription" });
        Assert.Null(ytSecondClaim);

        // General worker allowing feed-ingestion
        var feedClaimed = await queue.DequeueNextAsync("worker-general", TimeSpan.FromMinutes(5), new[] { "feed-ingestion" });
        Assert.NotNull(feedClaimed);
        Assert.Equal(feedTaskId, feedClaimed!.Id);
        Assert.Equal("feed-ingestion", feedClaimed.TaskType);
    }

    [Fact]
    public async Task DequeueNextAsync_WithWildcardAllowedTaskTypes_DequeuesAnyTask()
    {
        using var db = CreateDbContext();
        var queue = CreateQueue();

        var taskId = await queue.EnqueueAsync("special-task", new SamplePayload("Special", 100));

        var claimed = await queue.DequeueNextAsync("worker-wildcard", TimeSpan.FromMinutes(5), new[] { "*" });
        Assert.NotNull(claimed);
        Assert.Equal(taskId, claimed!.Id);
        Assert.Equal("special-task", claimed.TaskType);
    }
}
