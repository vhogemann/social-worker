using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.ReplyAgents;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class ReplyAgentPollingHostedServiceTests : SqliteTestBase
{
    [Fact]
    public async Task PollingLoop_SkipsAgent_WhenPolledWithinCheckInterval()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var agent = ReplyAgent.Create(
            userId: user.Id,
            name: "Hourly Twitter Agent",
            checkIntervalMinutes: 60,
            maxPostAgeDays: 3,
            minRankingScore: 1,
            autoPublishReplies: true,
            personaPrompt: "Be sharp."
        );
        agent.LastPolledAt = DateTime.UtcNow.AddMinutes(-15);
        db.ReplyAgents.Add(agent);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            RemoteId = "tweet-12345",
            Platform = "Twitter",
            PostedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        db.Posts.Add(post);

        var assignment = ReplyAgentPost.Create(agent.Id, post.Id);
        db.ReplyAgentPosts.Add(assignment);

        await db.SaveChangesAsync();

        var queue = new TestBackgroundTaskQueue();
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<IBackgroundTaskQueue>(queue);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReplyAgents:PollingIntervalMinutes"] = "1"
            })
            .Build();

        var service = new ReplyAgentPollingHostedService(
            scopeFactory,
            config,
            NullLogger<ReplyAgentPollingHostedService>.Instance);

        using var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);
        cts.Cancel();

        Assert.Empty(queue.EnqueuedTasks);
    }

    [Fact]
    public async Task PollingLoop_EnqueuesCheck_WhenCheckIntervalHasElapsed()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var agent = ReplyAgent.Create(
            userId: user.Id,
            name: "Hourly Twitter Agent",
            checkIntervalMinutes: 60,
            maxPostAgeDays: 3,
            minRankingScore: 1,
            autoPublishReplies: true,
            personaPrompt: "Be sharp."
        );
        agent.LastPolledAt = DateTime.UtcNow.AddMinutes(-70);
        db.ReplyAgents.Add(agent);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            RemoteId = "tweet-12345",
            Platform = "Twitter",
            PostedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        db.Posts.Add(post);

        var assignment = ReplyAgentPost.Create(agent.Id, post.Id);
        db.ReplyAgentPosts.Add(assignment);

        await db.SaveChangesAsync();

        var queue = new TestBackgroundTaskQueue();
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<IBackgroundTaskQueue>(queue);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReplyAgents:PollingIntervalMinutes"] = "1"
            })
            .Build();

        var service = new ReplyAgentPollingHostedService(
            scopeFactory,
            config,
            NullLogger<ReplyAgentPollingHostedService>.Instance);

        using var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await Task.Delay(300);
        await service.StopAsync(CancellationToken.None);
        cts.Cancel();

        Assert.NotEmpty(queue.EnqueuedTasks);
        Assert.Contains(queue.EnqueuedTasks, t => t.TaskType == "workflow-reply-check-post");
    }
}
