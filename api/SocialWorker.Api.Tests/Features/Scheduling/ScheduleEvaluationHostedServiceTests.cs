using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Scheduling.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Scheduling;

public sealed class ScheduleEvaluationHostedServiceTests : SqliteTestBase
{
    [Fact]
    public async Task EvaluateAsync_ExecutesSlotEvaluationSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<IBackgroundTaskQueue, TestBackgroundTaskQueue>();
        services.AddScoped<ScheduleSlotEvaluatorService>();

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = CreateSeedUser(db);
            var schedule = Schedule.Create(user.Id, "Periodic Active Schedule");
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        var hostedService = new ScheduleEvaluationHostedService(
            scopeFactory,
            NullLogger<ScheduleEvaluationHostedService>.Instance,
            TimeSpan.FromMilliseconds(50));

        var dispatched = await hostedService.EvaluateAsync(CancellationToken.None);

        Assert.Equal(0, dispatched);
    }
}
