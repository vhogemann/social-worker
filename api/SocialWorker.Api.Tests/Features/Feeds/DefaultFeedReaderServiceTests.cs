using System;
using System.Threading;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class DefaultFeedReaderServiceTests
{
    private sealed class FakeInstagramDiscoveryClient : IInstagramDiscoveryClient
    {
        public bool GetProfileFeedCalled { get; private set; }
        public string? TargetUsernameCalled { get; private set; }

        public Task<InstagramProfileDiscovery?> GetProfileWithMediaAsync(string callerIgUserId, string targetUsername, string accessToken, int limit = 10, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<string?> ResolveCallerIgUserIdAsync(string accessToken, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Feed> GetProfileFeedAsync(string targetUsername, string callerIgUserId, string accessToken, int limit = 10, CancellationToken ct = default)
        {
            GetProfileFeedCalled = true;
            TargetUsernameCalled = targetUsername;
            var feed = new Feed { Title = $"Feed for {targetUsername}" };
            return Task.FromResult(feed);
        }
    }

    private sealed class FakeInstagramCredentialProvider : IInstagramCredentialProvider
    {
        public InstagramCredentials? CredentialsToReturn { get; set; } = new("valid_token", "12345");

        public Task<InstagramCredentials?> GetCredentialsAsync(Guid? userId = null, CancellationToken ct = default)
        {
            return Task.FromResult(CredentialsToReturn);
        }
    }

    [Fact]
    public async Task ReadFeedAsync_InvalidInstagramUrl_ThrowsInvalidOperationException()
    {
        var service = new DefaultFeedReaderService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReadFeedAsync("instagram://"));
    }

    [Fact]
    public async Task ReadFeedAsync_InstagramUrl_WithoutRssHubAndWithoutCredentials_ThrowsInvalidOperationException()
    {
        var opts = Options.Create(new InstagramOptions
        {
            PreferRssHub = false
        });

        var credProvider = new FakeInstagramCredentialProvider { CredentialsToReturn = null };
        var client = new FakeInstagramDiscoveryClient();
        var service = new DefaultFeedReaderService(client, credProvider, opts, NullLogger<DefaultFeedReaderService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReadFeedAsync("instagram://someuser"));
    }

    [Fact]
    public async Task ReadFeedAsync_InstagramUrl_WithPreferRssHubFalse_CallsGraphApiClient()
    {
        var opts = Options.Create(new InstagramOptions
        {
            PreferRssHub = false
        });

        var credProvider = new FakeInstagramCredentialProvider();
        var client = new FakeInstagramDiscoveryClient();
        var service = new DefaultFeedReaderService(client, credProvider, opts, NullLogger<DefaultFeedReaderService>.Instance);

        var feed = await service.ReadFeedAsync("instagram://targetuser");

        Assert.NotNull(feed);
        Assert.True(client.GetProfileFeedCalled);
        Assert.Equal("targetuser", client.TargetUsernameCalled);
    }
}
