using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Scheduling;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class SchedulesEndpointTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user1;
    private readonly AppUser _user2;
    private readonly ClaimsPrincipal _principal1;
    private readonly ClaimsPrincipal _principal2;
    private readonly TestBackgroundTaskQueue _taskQueue;
    private readonly ScheduleQueueService _queueService;
    private readonly ScheduleSlotEvaluatorService _evaluator;

    public SchedulesEndpointTests()
    {
        _db = CreateDbContext();
        _user1 = CreateSeedUser(_db);
        _user2 = new AppUser { Id = Guid.NewGuid(), Username = "user2", Email = "user2@test.com", PasswordHash = "h" };
        _db.Users.Add(_user2);
        _db.SaveChanges();

        _principal1 = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _user1.Id.ToString()) }, "Test"));
        _principal2 = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _user2.Id.ToString()) }, "Test"));

        _taskQueue = new TestBackgroundTaskQueue();
        _queueService = new ScheduleQueueService(_db, _taskQueue);
        _evaluator = new ScheduleSlotEvaluatorService(_db, _taskQueue);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    [Fact]
    public async Task ScheduleCRUD_EndToEndFlow()
    {
        // 1. Create schedule
        var createSchedule = Schedule.Create(_user1.Id, "Morning Queue", "America/New_York");
        _db.Schedules.Add(createSchedule);
        await _db.SaveChangesAsync();

        var list = await _db.Schedules
            .Where(s => s.UserId == _user1.Id)
            .Include(s => s.SlotRules)
            .Include(s => s.QueueItems)
            .ToListAsync();

        Assert.Single(list);
        Assert.Equal("Morning Queue", list[0].Name);
        Assert.Equal("America/New_York", list[0].Timezone);

        // 2. Update schedule
        var schedule = await _db.Schedules.FindAsync(createSchedule.Id);
        Assert.NotNull(schedule);
        schedule.Name = "Updated Morning Queue";
        schedule.Timezone = "UTC";
        await _db.SaveChangesAsync();

        var updated = await _db.Schedules.FindAsync(createSchedule.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Morning Queue", updated.Name);
        Assert.Equal("UTC", updated.Timezone);

        // 3. Delete schedule
        _db.Schedules.Remove(updated);
        await _db.SaveChangesAsync();

        Assert.Null(await _db.Schedules.FindAsync(createSchedule.Id));
    }

    [Fact]
    public async Task SlotRules_CreationAndAutomaticCron()
    {
        var schedule = Schedule.Create(_user1.Id, "Social Buffer");
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        var start = new TimeOnly(9, 15);
        var end = new TimeOnly(10, 15);
        var cron = CronExpressionBuilder.Daily(start);

        var rule = ScheduleSlotRule.Create(
            schedule.Id,
            SlotRecurrenceType.Daily,
            cron,
            start,
            end,
            postCapacity: 4);

        _db.ScheduleSlotRules.Add(rule);
        await _db.SaveChangesAsync();

        var rules = await _db.ScheduleSlotRules.Where(r => r.ScheduleId == schedule.Id).ToListAsync();
        Assert.Single(rules);
        Assert.Equal("15 9 * * *", rules[0].CronExpression);
        Assert.Equal(4, rules[0].PostCapacity);

        _db.ScheduleSlotRules.Remove(rule);
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.ScheduleSlotRules.Where(r => r.ScheduleId == schedule.Id).ToListAsync());
    }

    [Fact]
    public async Task QueueManagement_EnqueueReorderPauseRemove()
    {
        var schedule = Schedule.Create(_user1.Id, "Buffer");
        _db.Schedules.Add(schedule);

        var draft1 = new Draft { Id = Guid.NewGuid(), UserId = _user1.Id, Title = "Draft 1", Content = "Text 1" };
        var thread1 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft1.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };
        var draft2 = new Draft { Id = Guid.NewGuid(), UserId = _user1.Id, Title = "Draft 2", Content = "Text 2" };
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Stage = PlatformThreadStage.Draft };

        _db.Drafts.AddRange(draft1, draft2);
        _db.PlatformThreads.AddRange(thread1, thread2);
        await _db.SaveChangesAsync();

        // 1. Enqueue
        var item1 = await _queueService.EnqueueAsync(schedule.Id, thread1.Id, draft1.Id);
        var item2 = await _queueService.EnqueueAsync(schedule.Id, thread2.Id, draft2.Id);

        Assert.Equal(0, item1.Position);
        Assert.Equal(1, item2.Position);

        // 2. Reorder
        var reordered = await _queueService.ReorderQueueAsync(schedule.Id, new[] { item2.Id, item1.Id });
        Assert.Equal(item2.Id, reordered[0].Id);
        Assert.Equal(0, reordered[0].Position);
        Assert.Equal(item1.Id, reordered[1].Id);
        Assert.Equal(1, reordered[1].Position);

        // 3. Pause
        var paused = await _queueService.PauseQueueItemAsync(schedule.Id, item1.Id, isPaused: true);
        Assert.NotNull(paused);
        Assert.Equal(ScheduleQueueItemStatus.Paused, paused.Status);

        // 4. Remove
        var removed = await _queueService.RemoveFromQueueAsync(schedule.Id, item2.Id);
        Assert.True(removed);

        var remaining = await _queueService.GetQueueAsync(schedule.Id);
        Assert.Single(remaining);
        Assert.Equal(item1.Id, remaining[0].Id);
        Assert.Equal(0, remaining[0].Position);
    }

    [Fact]
    public async Task TenantIsolation_UserCannotAccessOtherUsersSchedule()
    {
        var schedule = Schedule.Create(_user1.Id, "User 1 Schedule");
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        // User 2 query should find nothing
        var user2Schedules = await _db.Schedules
            .Where(s => s.UserId == _user2.Id && s.Id == schedule.Id)
            .ToListAsync();

        Assert.Empty(user2Schedules);
    }
}
