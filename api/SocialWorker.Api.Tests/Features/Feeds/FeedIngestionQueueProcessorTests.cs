using System;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Feeds;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedIngestionQueueProcessorTests
{
    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(5, 480)]
    [InlineData(10, 480)]
    public void ComputeRetryDelay_ReturnsExpectedBackoff(int attemptCount, double expectedSeconds)
    {
        var delay = FeedIngestionQueueProcessor.ComputeRetryDelay(attemptCount);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }
}
