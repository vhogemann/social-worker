using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Services;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddFeeds(this IHostApplicationBuilder builder)
    {
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
        builder.Services.AddScoped<FeedSourceIngestionService>();
        builder.Services.AddScoped<FeedThreadGenerationService>();
        builder.Services.AddScoped<DraftValidationService>();
        builder.Services.AddScoped<FeedAutoPublishService>();
        builder.Services.AddScoped<FeedOrchestrationService>();
        builder.Services.AddScoped<FeedPollingService>();
        builder.Services.AddSingleton<FeedPollingHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedPollingHostedService>());
    }
}
