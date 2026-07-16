using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddPublishing(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<PlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, BlueskyPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, TwitterPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, LinkedInPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, InstagramPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, FacebookPlatformContentPolicy>();
        builder.Services.AddSingleton<BlueskyContentValidator>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMaxCharactersRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMaxImagesRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyNoMixedImagesAndYouTubeRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyUnsupportedMarkdownRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderLinkRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderMediaRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderUrlRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMissingAltTextRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyTitleLikeOpenerRule>();
        builder.Services.AddScoped<BlueskyDraftValidator>();
        builder.Services.AddHttpClient<IPublisher, BlueskyPublisher>();
        builder.Services.AddHttpClient<IBlueskyReplyTargetResolver, BlueskyReplyTargetResolver>();
        builder.Services.AddScoped<IPublisher, TwitterPublisher>();
        builder.Services.AddScoped<IPublisher, LinkedInPublisher>();
        builder.Services.AddScoped<IPublisher, FacebookPublisher>();
        builder.Services.AddScoped<IPublisher, InstagramPublisher>();
        builder.Services.AddScoped<IPublisherResolver, PublisherResolver>();
    }
}
