using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SocialWorker.Api.Infrastructure.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure;

public sealed class AppUrlResolverTests
{
    [Fact]
    public void BaseUrl_EndingWithApi_NormalizesWebAndApiUrls()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = "https://social.plopsaurus.xyz/api"
            })
            .Build();

        var resolver = new AppUrlResolver(config);

        Assert.Equal("https://social.plopsaurus.xyz", resolver.GetWebBaseUrl());
        Assert.Equal("https://social.plopsaurus.xyz/api", resolver.GetApiBaseUrl());

        var mediaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.Equal("https://social.plopsaurus.xyz/api/media/11111111-1111-1111-1111-111111111111", resolver.GetMediaUrl(mediaId));

        var draftId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.Equal("https://social.plopsaurus.xyz/draft/22222222-2222-2222-2222-222222222222", resolver.GetDraftWebUrl(draftId));
        Assert.Equal("https://social.plopsaurus.xyz/api/drafts/22222222-2222-2222-2222-222222222222/preview", resolver.GetDraftPreviewHtmlUrl(draftId));
    }

    [Fact]
    public void BaseUrl_WithoutApi_NormalizesWebAndApiUrls()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_BASE_URL"] = "https://social.plopsaurus.xyz/"
            })
            .Build();

        var resolver = new AppUrlResolver(config);

        Assert.Equal("https://social.plopsaurus.xyz", resolver.GetWebBaseUrl());
        Assert.Equal("https://social.plopsaurus.xyz/api", resolver.GetApiBaseUrl());

        var mediaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.Equal("https://social.plopsaurus.xyz/api/media/11111111-1111-1111-1111-111111111111", resolver.GetMediaUrl(mediaId));
    }

    [Fact]
    public void FallbackToMetaThreadsPublicBaseUrl_WhenAppBaseUrlNotSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetaThreads:PublicBaseUrl"] = "https://threads.custom.domain"
            })
            .Build();

        var resolver = new AppUrlResolver(config);

        Assert.Equal("https://threads.custom.domain", resolver.GetWebBaseUrl());
        Assert.Equal("https://threads.custom.domain/api", resolver.GetApiBaseUrl());
    }

    [Fact]
    public void DefaultFallback_WhenNoConfigurationPresent()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new AppUrlResolver(config);

        Assert.Equal("http://localhost:8100", resolver.GetWebBaseUrl());
        Assert.Equal("http://localhost:8100/api", resolver.GetApiBaseUrl());
    }
}
