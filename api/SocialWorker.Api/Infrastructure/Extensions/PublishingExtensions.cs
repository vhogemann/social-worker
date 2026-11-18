using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Instagram;
using SocialWorker.Api.Features.Publishing.Instagram.Validation;
using SocialWorker.Api.Features.Publishing.Mastodon;
using SocialWorker.Api.Features.Publishing.Mastodon.Validation;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Features.Accounts.Tokens;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddPublishing(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<LinkedInOptions>(builder.Configuration.GetSection(LinkedInOptions.SectionName));
        builder.Services.Configure<MetaThreadsOptions>(builder.Configuration.GetSection(MetaThreadsOptions.SectionName));
        builder.Services.Configure<TwitterOptions>(builder.Configuration.GetSection(TwitterOptions.SectionName));
        builder.Services.Configure<MastodonOptions>(builder.Configuration.GetSection(MastodonOptions.SectionName));
        builder.Services.AddHttpClient<MetaThreadsOAuthService>();
        builder.Services.AddHttpClient<TwitterOAuthService>();
        builder.Services.AddHttpClient<MastodonOAuthService>();
        builder.Services.AddScoped<ITokenRefresher, MetaThreadsTokenRefresher>();
        builder.Services.AddScoped<ITokenRefresher, TwitterTokenRefresher>();
        builder.Services.AddScoped<ITokenRefresher, InstagramTokenRefresher>();
        builder.Services.AddHostedService<TokenRefreshHostedService>();
        builder.Services.AddSingleton<PlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, BlueskyPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, TwitterPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, LinkedInPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, InstagramPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, FacebookPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, MetaThreadsPlatformContentPolicy>();
        builder.Services.AddSingleton<IPlatformContentPolicy, MastodonPlatformContentPolicy>();
        builder.Services.AddSingleton<BlueskyContentValidator>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMaxCharactersRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMaxImagesRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyNoMixedImagesAndYouTubeRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyUnsupportedMarkdownRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderLinkRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderMediaRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderUrlRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMissingAltTextRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyYouTubeEmbedSyntaxRule>();
        builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyTitleLikeOpenerRule>();
        builder.Services.AddScoped<BlueskyDraftValidator>();
        builder.Services.AddScoped<CanonicalDraftValidator>();
        builder.Services.AddScoped<LinkedInDraftValidator>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Publishing.MetaThreads.Validation.MetaThreadsDraftValidator>();
        builder.Services.AddScoped<SocialWorker.Api.Features.Publishing.Twitter.Validation.TwitterDraftValidator>();
        builder.Services.AddScoped<InstagramDraftValidator>();
        builder.Services.AddScoped<MastodonDraftValidator>();
        builder.Services.AddScoped<IDraftValidatorResolver, DraftValidatorResolver>();
        builder.Services.AddScoped<PublishValidationService>();
        builder.Services.AddScoped<PublishOutcomeRecorder>();
        builder.Services.AddSingleton<IPublishLockService, PublishLockService>();
        builder.Services.AddHttpClient<BlueskyApiClient>();
        builder.Services.AddHttpClient<BlueskyContentPreparationService>();
        builder.Services.AddScoped<BlueskyFacetBuilder>();
        builder.Services.AddScoped<IPublisher, BlueskyPublisher>();
        builder.Services.AddHttpClient<IBlueskyReplyTargetResolver, BlueskyReplyTargetResolver>();
        builder.Services.AddHttpClient<TwitterApiClient>();
        builder.Services.AddScoped<IPublisher, TwitterPublisher>();
        builder.Services.AddHttpClient<LinkedInApiClient>();
        builder.Services.AddHttpClient<LinkedInOAuthService>();
        builder.Services.AddScoped<LinkedInPublishService>();
        builder.Services.AddScoped<IPublisher, LinkedInPublisher>();
        builder.Services.AddHttpClient<MetaThreadsApiClient>();
        builder.Services.AddScoped<IPublisher, MetaThreadsPublisher>();
        builder.Services.AddHttpClient<InstagramApiClient>();
        builder.Services.AddScoped<IPublisher, InstagramPublisher>();
        builder.Services.AddHttpClient<MastodonApiClient>();
        builder.Services.AddScoped<IPublisher, MastodonPublisher>();
        builder.Services.AddScoped<IPublisher, FacebookPublisher>();
        builder.Services.AddScoped<IPublisherResolver, PublisherResolver>();
    }
}
