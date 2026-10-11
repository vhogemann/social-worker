using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.ReplyAgents;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Adapters;
using SocialWorker.Api.Features.ReplyAgents.Handlers;
using SocialWorker.Api.Features.ReplyAgents.Services;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddReplyAgents(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IReplyPlatformAdapter, BlueskyReplyAdapter>();
        builder.Services.AddScoped<IReplyPlatformAdapter, MetaThreadsReplyAdapter>();
        builder.Services.AddScoped<IReplyPlatformAdapter, TwitterReplyAdapter>();
        builder.Services.AddScoped<IReplyPlatformAdapter, LinkedInReplyAdapter>();
        builder.Services.AddScoped<IReplyPlatformAdapterResolver, ReplyPlatformAdapterResolver>();

        builder.Services.AddScoped<ICommentEnrichmentService, CommentEnrichmentService>();
        builder.Services.AddScoped<ICommentRankingService, CommentRankingService>();
        builder.Services.AddScoped<IReplyGenerationService, ReplyGenerationService>();
        builder.Services.AddScoped<ICommentThreadSummarizer, CommentThreadSummarizer>();
        builder.Services.AddScoped<ReplyAgentCoordinatorService>();

        builder.Services.AddScoped<IBackgroundTaskHandler, WorkflowReplyCheckPostTaskHandler>();
        builder.Services.AddScoped<IBackgroundTaskHandler, ReplyThreadSummaryTaskHandler>();

        builder.Services.AddHostedService<ReplyAgentPollingHostedService>();
    }
}
