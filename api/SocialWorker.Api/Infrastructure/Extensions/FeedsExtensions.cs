using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Feeds.Services;
using SocialWorker.Api.Features.Workflows.Services;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddFeeds(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<RssHubOptions>(builder.Configuration.GetSection(RssHubOptions.SectionName));
        builder.Services.AddSingleton<IRssHubRouteResolver, RssHubRouteResolver>();

        builder.Services.AddHttpClient<YouTubeFeedDiscoveryService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddHttpClient<RssFeedDiscoveryService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddHttpClient<FeedDiscoveryService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddScoped<RssHubFeedDiscoveryService>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Sources.Instagram.InstagramFeedDiscoveryService>();
        builder.Services.AddScoped<IFeedReaderService, DefaultFeedReaderService>();
        builder.Services.AddScoped<FeedSourceIngestionService>();
        builder.Services.AddScoped<FeedThreadGenerationService>();
        builder.Services.AddScoped<DraftValidationService>();
        builder.Services.AddScoped<FeedAutoPublishService>();
        builder.Services.AddScoped<FeedOrchestrationService>();
        builder.Services.AddScoped<FeedPollingService>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Workflows.Services.ISourceRelevanceEvaluator, SocialWorker.Api.Features.Workflows.Services.SourceRelevanceEvaluator>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Workflows.Abstractions.IWorkflowSourceHandler, FeedWorkflowSourceHandler>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Workflows.Abstractions.IWorkflowSourceHandler, DigestWorkflowSourceHandler>();
        builder.Services.AddSingleton<FeedPollingHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedPollingHostedService>());
    }
}
