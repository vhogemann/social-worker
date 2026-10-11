using System;
using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources.Instagram;

public sealed class InstagramFeedDiscoveryServiceTests
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
    [InlineData("https://www.instagram.com/flaviobolsonaro", true)]
    [InlineData("https://instagram.com/jairmessiasbolsonaro/", true)]
    [InlineData("instagram://lulaoficial", true)]
    [InlineData("https://youtube.com/channel/123", false)]
    public void CanHandle_ReturnsExpected(string url, bool expected)
    {
        var service = new InstagramFeedDiscoveryService();
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u : new Uri("https://dummy.com");
        var result = service.CanHandle(uri, url);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DiscoverAsync_NormalizesUrl_AndEnrichesWithProfileIfAvailable()
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

        var service = new InstagramFeedDiscoveryService(client, creds);

        var url = "https://www.instagram.com/flaviobolsonaro";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("instagram://flaviobolsonaro", result.FeedUrl);
        Assert.Equal("Flávio Bolsonaro (@flaviobolsonaro) (Instagram)", result.Title);
        Assert.Equal("https://www.instagram.com/flaviobolsonaro", result.WebsiteUrl);
    }

    [Fact]
    public async Task DiscoverAsync_WithoutCredentials_ReturnsBasicDiscoveryResult()
    {
        var service = new InstagramFeedDiscoveryService();

        var url = "https://instagram.com/lulaoficial";
        var result = await service.DiscoverAsync(url, new Uri(url));

        Assert.True(result.Success);
        Assert.Equal("instagram://lulaoficial", result.FeedUrl);
        Assert.Equal("@lulaoficial (Instagram)", result.Title);
        Assert.Equal("https://www.instagram.com/lulaoficial", result.WebsiteUrl);
    }
}
