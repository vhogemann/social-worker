using SocialWorker.Api.Features.Feeds;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddFeeds(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<FeedDiscoveryService>();
        builder.Services.AddScoped<FeedOrchestrationService>();
        builder.Services.AddSingleton<FeedPollingHostedService>();
        builder.Services.AddSingleton<FeedIngestionQueueHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedPollingHostedService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedIngestionQueueHostedService>());
    }
}
