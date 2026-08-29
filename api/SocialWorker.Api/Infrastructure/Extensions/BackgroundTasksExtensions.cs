using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Services;
using SocialWorker.Api.Features.Drafts.Handlers;
using SocialWorker.Api.Features.Feeds.Handlers;
using SocialWorker.Api.Features.Sources.Handlers;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddBackgroundTasks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IBackgroundTaskQueue, PostgresBackgroundTaskQueue>();

        builder.Services.AddScoped<SourceTranscriptionTaskHandler>();
        builder.Services.AddScoped<IBackgroundTaskHandler, SourceTranscriptionTaskHandler>();

        builder.Services.AddScoped<SourceScrapeTaskHandler>();
        builder.Services.AddScoped<IBackgroundTaskHandler, SourceScrapeTaskHandler>();

        builder.Services.AddScoped<DraftChatSummaryTaskHandler>();
        builder.Services.AddScoped<IBackgroundTaskHandler, DraftChatSummaryTaskHandler>();

        builder.Services.AddScoped<FeedIngestionTaskHandler>();
        builder.Services.AddScoped<IBackgroundTaskHandler, FeedIngestionTaskHandler>();

        builder.Services.AddSingleton<ITaskHandlerResolver>(sp =>
        {
            var handlers = sp.GetServices<IBackgroundTaskHandler>();
            return new TaskHandlerResolver(handlers);
        });

        builder.Services.AddHostedService<BackgroundTaskHostedService>();
        builder.Services.AddHostedService<BackgroundTaskRetentionService>();
    }
}
