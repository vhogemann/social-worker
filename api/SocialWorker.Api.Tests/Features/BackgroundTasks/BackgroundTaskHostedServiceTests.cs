using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.BackgroundTasks.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.BackgroundTasks;

public sealed class BackgroundTaskHostedServiceTests : SqliteTestBase
{
    private sealed record TestJobPayload(string Message);

    private sealed class TestJobHandler : BackgroundTaskHandlerBase<TestJobPayload>
    {
        private readonly Func<TestJobPayload, Task<TaskResult>> _execute;

        public override string TaskType => "test-job";

        public TestJobHandler(Func<TestJobPayload, Task<TaskResult>> execute)
        {
            _execute = execute;
        }

        public override Task<TaskResult> ExecuteAsync(TestJobPayload payload, ITaskExecutionContext context, CancellationToken ct)
        {
            return _execute(payload);
        }
    }

    [Fact]
    public async Task WorkerPool_ProcessesTaskSuccessfully()
    {
        var handler = new TestJobHandler(p => Task.FromResult(TaskResult.Success()));

        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<TestJobHandler>(handler);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var queue = new PostgresBackgroundTaskQueue(scopeFactory, NullLogger<PostgresBackgroundTaskQueue>.Instance);
        var resolver = new TaskHandlerResolver(new IBackgroundTaskHandler[] { handler });

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundTasks:Workers:0:Name"] = "test-worker",
                ["BackgroundTasks:Workers:0:AllowedTaskTypes:0"] = "test-job"
            })
            .Build();

        var hostedService = new BackgroundTaskHostedService(
            queue,
            resolver,
            scopeFactory,
            NullLogger<BackgroundTaskHostedService>.Instance,
            configuration);

        using var cts = new CancellationTokenSource();
        _ = hostedService.StartAsync(cts.Token);

        var taskId = await queue.EnqueueAsync("test-job", new TestJobPayload("Hello Worker"));

        BackgroundTask? stored = null;
        for (int i = 0; i < 50; i++)
        {
            try
            {
                using var verifyDb = CreateDbContext();
                stored = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    verifyDb.BackgroundTasks.AsNoTracking(), t => t.Id == taskId);
                if (stored?.Status == BackgroundTaskStatus.Completed)
                    break;
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
            }
            await Task.Delay(100);
        }

        await hostedService.StopAsync(CancellationToken.None);
        cts.Cancel();

        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Completed, stored!.Status);
    }

    [Fact]
    public async Task WorkerPool_HandlesFailingHandler_WithoutCrashing()
    {
        var handler = new TestJobHandler(p => throw new InvalidOperationException("Simulated crash in handler"));

        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<TestJobHandler>(handler);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var queue = new PostgresBackgroundTaskQueue(scopeFactory, NullLogger<PostgresBackgroundTaskQueue>.Instance);
        var resolver = new TaskHandlerResolver(new IBackgroundTaskHandler[] { handler });

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundTasks:Workers:0:Name"] = "test-worker",
                ["BackgroundTasks:Workers:0:AllowedTaskTypes:0"] = "test-job"
            })
            .Build();

        var hostedService = new BackgroundTaskHostedService(
            queue,
            resolver,
            scopeFactory,
            NullLogger<BackgroundTaskHostedService>.Instance,
            configuration);

        using var cts = new CancellationTokenSource();
        _ = hostedService.StartAsync(cts.Token);

        var taskId = await queue.EnqueueAsync("test-job", new TestJobPayload("Boom"));

        BackgroundTask? stored = null;
        for (int i = 0; i < 50; i++)
        {
            try
            {
                using var verifyDb = CreateDbContext();
                stored = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    verifyDb.BackgroundTasks.AsNoTracking(), t => t.Id == taskId);
                if (stored?.Status == BackgroundTaskStatus.Pending && stored.LastError != null)
                    break;
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
            }
            await Task.Delay(100);
        }

        await hostedService.StopAsync(CancellationToken.None);
        cts.Cancel();

        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Pending, stored!.Status);
        Assert.Contains("Simulated crash in handler", stored.LastError);
    }

    [Fact]
    public async Task WorkerPool_RespectsAllowedTaskTypes_DoesNotProcessUnassignedTasks()
    {
        var handler = new TestJobHandler(p => Task.FromResult(TaskResult.Success()));

        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<TestJobHandler>(handler);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var queue = new PostgresBackgroundTaskQueue(scopeFactory, NullLogger<PostgresBackgroundTaskQueue>.Instance);
        var resolver = new TaskHandlerResolver(new IBackgroundTaskHandler[] { handler });

        // Worker only allows "other-job", so "test-job" should remain untouched
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundTasks:Workers:0:Name"] = "other-worker",
                ["BackgroundTasks:Workers:0:AllowedTaskTypes:0"] = "other-job"
            })
            .Build();

        var hostedService = new BackgroundTaskHostedService(
            queue,
            resolver,
            scopeFactory,
            NullLogger<BackgroundTaskHostedService>.Instance,
            configuration);

        using var cts = new CancellationTokenSource();
        _ = hostedService.StartAsync(cts.Token);

        var taskId = await queue.EnqueueAsync("test-job", new TestJobPayload("Untouched"));

        await Task.Delay(500);

        using var verifyDb = CreateDbContext();
        var stored = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            verifyDb.BackgroundTasks.AsNoTracking(), t => t.Id == taskId);

        await hostedService.StopAsync(CancellationToken.None);
        cts.Cancel();

        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Pending, stored!.Status);
        Assert.Equal(0, stored.AttemptCount);
    }
}
