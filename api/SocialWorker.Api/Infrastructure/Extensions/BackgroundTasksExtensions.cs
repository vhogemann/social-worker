using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Services;
using SocialWorker.Api.Features.Drafts.Handlers;
using SocialWorker.Api.Features.Feeds.Handlers;
using SocialWorker.Api.Features.Scheduling.Handlers;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Features.Sources.Handlers;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddBackgroundTasks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IBackgroundTaskQueue, PostgresBackgroundTaskQueue>();

        builder.Services.AddScoped<SourceTranscriptionTaskHandler>();
        builder.Services.AddScoped<SourceScrapeTaskHandler>();
        builder.Services.AddScoped<DraftChatSummaryTaskHandler>();
        builder.Services.AddScoped<FeedIngestionTaskHandler>();
        builder.Services.AddScoped<ScheduledPublishingTaskHandler>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Workflows.Handlers.WorkflowDraftRetryTaskHandler>();

        builder.Services.AddScoped<IScheduleQueueService, ScheduleQueueService>();
        builder.Services.AddScoped<ScheduleSlotEvaluatorService>();
        builder.Services.AddScoped<ICalendarProjectionService, CalendarProjectionService>();

        builder.Services.AddScoped<SocialWorker.Api.Features.ReplyAgents.Handlers.WorkflowReplyCheckPostTaskHandler>();
        builder.Services.AddScoped<SocialWorker.Api.Features.ReplyAgents.Handlers.ReplyThreadSummaryTaskHandler>();

        var resolver = new TaskHandlerResolver();
        resolver.Register<SourceTranscriptionTaskHandler>("source-transcription", TimeSpan.FromMinutes(15));
        resolver.Register<SourceScrapeTaskHandler>("source-scrape", TimeSpan.FromMinutes(3));
        resolver.Register<DraftChatSummaryTaskHandler>("draft-chat-summary", TimeSpan.FromMinutes(3));
        resolver.Register<FeedIngestionTaskHandler>("feed-ingestion", TimeSpan.FromMinutes(5));
        resolver.Register<ScheduledPublishingTaskHandler>(ScheduledPublishingTaskHandler.TaskTypeName, TimeSpan.FromMinutes(5));
        resolver.Register<SocialWorker.Api.Features.Workflows.Handlers.WorkflowDraftRetryTaskHandler>(SocialWorker.Api.Features.Workflows.Handlers.WorkflowDraftRetryTaskHandler.TaskTypeName, TimeSpan.FromMinutes(5));
        resolver.Register<SocialWorker.Api.Features.ReplyAgents.Handlers.WorkflowReplyCheckPostTaskHandler>("workflow-reply-check-post", TimeSpan.FromMinutes(5));
        resolver.Register<SocialWorker.Api.Features.ReplyAgents.Handlers.ReplyThreadSummaryTaskHandler>("reply-thread-summary", TimeSpan.FromMinutes(5));

        builder.Services.AddSingleton<ITaskHandlerResolver>(resolver);

        builder.Services.AddHostedService<BackgroundTaskHostedService>();
        builder.Services.AddHostedService<BackgroundTaskRetentionService>();
        builder.Services.AddHostedService<SocialWorker.Api.Features.Scheduling.Services.ScheduleEvaluationHostedService>();
    }
}
