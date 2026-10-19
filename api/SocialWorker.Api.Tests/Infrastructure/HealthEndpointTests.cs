using System;
using SocialWorker.Api.Infrastructure;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure;

public sealed class HealthEndpointTests
{
    [Fact]
    public void GetHealth_ReturnsHealthyStatusAndRecentTimestamp()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var health = HealthEndpoint.GetHealth();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.Equal("healthy", health.Status);
        Assert.InRange(health.Timestamp, before, after);
    }
}
