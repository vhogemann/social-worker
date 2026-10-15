using System;
using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using SocialWorker.Api.Features.Feeds.Services;
using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class RssHubFeedDiscoveryServiceTests
{
    private sealed class FakeInstagramDiscoveryClient : IInstagramDiscoveryClient
    {
        public InstagramProfileDiscovery? ProfileToReturn { get; set; }

        public Task<InstagramProfileDiscovery?> GetProfileWithMediaAsync(string callerIgUserId, string targetUsername, string accessToken, int limit = 10, CancellationToken ct = default)
        {
            return Task.FromResult(ProfileToReturn);
        }

        public Task<string?> ResolveCallerIgUserIdAsync(string accessToken, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<Feed> GetProfileFeedAsync(string targetUsername, string callerIgUserId, string accessToken, int limit = 10, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeInstagramCredentialProvider : IInstagramCredentialProvider
    {
        public InstagramCredentials? CredentialsToReturn { get; set; }

        public Task<InstagramCredentials?> GetCredentialsAsync(Guid? userId = null, CancellationToken ct = default)
        {
            return Task.FromResult(CredentialsToReturn);
        }
    }

    [Theory]
    [InlineData("https://www.instagram.com/hilton_erika", true)]
    [InlineData("instagram://hilton_erika", true)]
    [InlineData("https://x.com/elonmusk", true)]
    [InlineData("x://elonmusk", true)]
    [InlineData("https://t.me/durov", true)]
    [InlineData("telegram://durov", true)]
    [InlineData("https://reddit.com/r/technology", true)]
    [InlineData("https://bsky.app/profile/erikahilton.bsky.social", true)]
    [InlineData("https://youtube.com/channel/123", false)]
    [InlineData("https://example.com/blog", false)]
    public void CanHandle_ReturnsExpected(string url, bool expected)
    {
        var service = new RssHubFeedDiscoveryService();
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u : new Uri("https://dummy.local");
        var result = service.CanHandle(uri, url);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DiscoverAsync_InstagramWithGraphApiFallback_EnrichesTitleWhenRssHubCold()
    {
        var client = new FakeInstagramDiscoveryClient
        {
            ProfileToReturn = new InstagramProfileDiscovery(
                Id: "1784140001",
                Username: "flaviobolsonaro",
                Name: "Flávio Bolsonaro",
                ProfilePictureUrl: null,
                Biography: null,
                Media: null)
        };

        var creds = new FakeInstagramCredentialProvider
        {
            CredentialsToReturn = new InstagramCredentials("token", "1784149999")
        };

        var service = new RssHubFeedDiscoveryService(new RssHubRouteResolver(), client, creds);

        var url = "https://www.instagram.com/flaviobolsonaro";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("instagram://flaviobolsonaro", result.FeedUrl);
        Assert.Equal("Flávio Bolsonaro (@flaviobolsonaro) (Instagram)", result.Title);
        Assert.Equal("https://www.instagram.com/flaviobolsonaro", result.WebsiteUrl);
    }

    [Fact]
    public async Task DiscoverAsync_TwitterWithoutProbe_ReturnsOptimisticSyntheticTitle()
    {
        var service = new RssHubFeedDiscoveryService();

        var url = "https://x.com/elonmusk";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("x://elonmusk", result.FeedUrl);
        Assert.Equal("@elonmusk (Twitter)", result.Title);
        Assert.Equal("https://x.com/elonmusk", result.WebsiteUrl);
    }

    [Fact]
    public async Task DiscoverAsync_TelegramWithoutProbe_ReturnsOptimisticSyntheticTitle()
    {
        var service = new RssHubFeedDiscoveryService();

        var url = "https://t.me/durov";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("telegram://durov", result.FeedUrl);
        Assert.Equal("@durov (Telegram)", result.Title);
        Assert.Equal("https://t.me/durov", result.WebsiteUrl);
    }

    [Fact]
    public async Task DiscoverAsync_RedditWithoutProbe_ReturnsOptimisticSyntheticTitle()
    {
        var service = new RssHubFeedDiscoveryService();

        var url = "https://www.reddit.com/r/nonexistent_sub_xyz_99999";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("reddit://r/nonexistent_sub_xyz_99999", result.FeedUrl);
        Assert.Equal("@r/nonexistent_sub_xyz_99999 (Reddit)", result.Title);
        Assert.Equal("https://www.reddit.com/r/nonexistent_sub_xyz_99999", result.WebsiteUrl);
    }

    [Fact]
    public async Task DiscoverAsync_RedditSubreddit_DiscoversAndIncludesPlatform()
    {
        var service = new RssHubFeedDiscoveryService();

        var url = "https://www.reddit.com/r/technology";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("reddit://r/technology", result.FeedUrl);
        Assert.Contains("Reddit", result.Title);
        Assert.Equal("https://www.reddit.com/r/technology", result.WebsiteUrl);
    }
}
