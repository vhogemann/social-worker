using System;
using SocialWorker.Api.Features.Feeds;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedSourceIngestionServiceTests
{
    [Theory]
    [InlineData("Tech News", "Some tech description", null, null, true)]
    [InlineData("Tech News", "Some tech description", "Tech", null, true)]
    [InlineData("Sports News", "Some sports description", "Tech", null, false)]
    [InlineData("Tech News", "Contains Crypto scam", null, "Crypto", false)]
    [InlineData("Tech News", "Normal post", "Tech", "Crypto", true)]
    public void PassesFilters_EvaluatesIncludesAndExcludesCorrectly(
        string title,
        string description,
        string? includeFilters,
        string? excludeFilters,
        bool expected)
    {
        var result = FeedSourceIngestionService.PassesFilters(title, description, includeFilters, excludeFilters);
        Assert.Equal(expected, result);
    }
}
