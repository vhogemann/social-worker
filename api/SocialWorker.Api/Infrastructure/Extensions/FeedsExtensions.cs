using SocialWorker.Api.Features.Feeds;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddFeeds(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<YouTubeFeedDiscoveryService>();
        builder.Services.AddHttpClient<RssFeedDiscoveryService>();
        builder.Services.AddHttpClient<FeedDiscoveryService>();
        builder.Services.AddScoped<FeedSourceIngestionService>();
        builder.Services.AddScoped<FeedThreadGenerationService>();
        builder.Services.AddScoped<FeedAutoPublishService>();
        builder.Services.AddScoped<FeedOrchestrationService>();
        builder.Services.AddScoped<FeedIngestionQueueProcessor>();
        builder.Services.AddSingleton<FeedPollingHostedService>();
        builder.Services.AddSingleton<FeedIngestionQueueHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedPollingHostedService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedIngestionQueueHostedService>());
    }
}
