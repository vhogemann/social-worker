using System;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Features.Feeds.Services;

namespace SocialWorker.Api.Features.Sources.Instagram;

public class InstagramFeedDiscoveryService : RssHubFeedDiscoveryService
{
    public InstagramFeedDiscoveryService(
        IInstagramDiscoveryClient? discoveryClient = null,
        IInstagramCredentialProvider? credentialProvider = null,
        ILogger<InstagramFeedDiscoveryService>? logger = null)
        : base(new RssHubRouteResolver(), discoveryClient, credentialProvider, null)
    {
    }

    public InstagramFeedDiscoveryService(
        IRssHubRouteResolver routeResolver,
        IInstagramDiscoveryClient? discoveryClient = null,
        IInstagramCredentialProvider? credentialProvider = null,
        ILogger<InstagramFeedDiscoveryService>? logger = null)
        : base(routeResolver, discoveryClient, credentialProvider, null)
    {
    }
}
