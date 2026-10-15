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

public sealed class BackgroundTaskRetentionServiceTests : SqliteTestBase
{
    [Fact]
    public async Task PruneOldTasksAsync_PrunesOldCompletedTasksAndFinishedQueueItems()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        Guid oldQueueItemId;
        Guid freshQueueItemId;
        Guid oldTaskId;

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = CreateSeedUser(db);
            var schedule = Schedule.Create(user.Id, "Retention Test Schedule");
            db.Schedules.Add(schedule);

            var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Draft", Content = "Text" };
            db.Drafts.Add(draft);

            var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Scheduled };
            db.PlatformThreads.Add(thread);

            var oldFinishedItem = ScheduleQueueItem.Create(schedule.Id, draft.Id, thread.Id, 0, 7);
            oldFinishedItem.Status = ScheduleQueueItemStatus.Succeeded;
            oldFinishedItem.UpdatedAt = DateTime.UtcNow.AddDays(-10);
            oldQueueItemId = oldFinishedItem.Id;

            var freshItem = ScheduleQueueItem.Create(schedule.Id, draft.Id, thread.Id, 1, 7);
            freshItem.Status = ScheduleQueueItemStatus.Queued;
            freshItem.UpdatedAt = DateTime.UtcNow;
            freshQueueItemId = freshItem.Id;

            db.ScheduleQueueItems.AddRange(oldFinishedItem, freshItem);

            var oldTask = new BackgroundTask
            {
                Id = Guid.NewGuid(),
                TaskType = "test",
                Status = BackgroundTaskStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddHours(-48),
                CreatedAt = DateTime.UtcNow.AddHours(-49),
                UpdatedAt = DateTime.UtcNow.AddHours(-48),
                PayloadJson = "{}"
            };
            oldTaskId = oldTask.Id;
            db.BackgroundTasks.Add(oldTask);

            await db.SaveChangesAsync();
        }

        var service = new BackgroundTaskRetentionService(scopeFactory, NullLogger<BackgroundTaskRetentionService>.Instance);
        var prunedCount = await service.PruneOldTasksAsync(CancellationToken.None);

        Assert.True(prunedCount >= 2);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.ScheduleQueueItems.FindAsync(oldQueueItemId));
            Assert.NotNull(await db.ScheduleQueueItems.FindAsync(freshQueueItemId));
            Assert.Null(await db.BackgroundTasks.FindAsync(oldTaskId));
        }
    }
}
