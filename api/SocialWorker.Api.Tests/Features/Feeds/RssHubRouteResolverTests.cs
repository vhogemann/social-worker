using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Feeds.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class RssHubRouteResolverTests
{
    private readonly RssHubRouteResolver _resolver = new();

    [Theory]
    [InlineData("https://www.instagram.com/hilton_erika", "Instagram", "/instagram/user/hilton_erika", "instagram://hilton_erika", "https://www.instagram.com/hilton_erika")]
    [InlineData("instagram://hilton_erika", "Instagram", "/instagram/user/hilton_erika", "instagram://hilton_erika", "https://www.instagram.com/hilton_erika")]
    [InlineData("https://instagram.com/rickazevedor/", "Instagram", "/instagram/user/rickazevedor", "instagram://rickazevedor", "https://www.instagram.com/rickazevedor")]
    public void TryResolve_Instagram_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Theory]
    [InlineData("https://x.com/elonmusk", "Twitter", "/x/user/elonmusk", "x://elonmusk", "https://x.com/elonmusk")]
    [InlineData("https://twitter.com/jack/", "Twitter", "/x/user/jack", "x://jack", "https://x.com/jack")]
    [InlineData("x://glenngreenwald", "Twitter", "/x/user/glenngreenwald", "x://glenngreenwald", "https://x.com/glenngreenwald")]
    public void TryResolve_Twitter_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Theory]
    [InlineData("https://t.me/s/durov", "Telegram", "/telegram/channel/durov", "telegram://durov", "https://t.me/durov")]
    [InlineData("https://t.me/medoedelirio", "Telegram", "/telegram/channel/medoedelirio", "telegram://medoedelirio", "https://t.me/medoedelirio")]
    [InlineData("telegram://news_channel", "Telegram", "/telegram/channel/news_channel", "telegram://news_channel", "https://t.me/news_channel")]
    public void TryResolve_Telegram_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Theory]
    [InlineData("https://www.reddit.com/r/technology", "Reddit", "/reddit/subreddit/technology", "reddit://r/technology", "https://www.reddit.com/r/technology")]
    [InlineData("reddit://r/brasil", "Reddit", "/reddit/subreddit/brasil", "reddit://r/brasil", "https://www.reddit.com/r/brasil")]
    public void TryResolve_RedditSubreddit_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@tiktokcreators", "TikTok", "/tiktok/user/tiktokcreators", "tiktok://tiktokcreators", "https://www.tiktok.com/@tiktokcreators")]
    [InlineData("tiktok://someone", "TikTok", "/tiktok/user/someone", "tiktok://someone", "https://www.tiktok.com/@someone")]
    public void TryResolve_TikTok_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Theory]
    [InlineData("https://bsky.app/profile/erikahilton.bsky.social", "Bluesky", "/bsky/profile/erikahilton.bsky.social", "bluesky://erikahilton.bsky.social", "https://bsky.app/profile/erikahilton.bsky.social")]
    [InlineData("bluesky://jay.bsky.team", "Bluesky", "/bsky/profile/jay.bsky.team", "bluesky://jay.bsky.team", "https://bsky.app/profile/jay.bsky.team")]
    public void TryResolve_Bluesky_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "YouTubePlaylist", "/youtube/playlist/PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "https://www.youtube.com/feeds/videos.xml?playlist_id=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "https://www.youtube.com/playlist?list=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp")]
    [InlineData("https://youtube.com/playlist?list=PL12345&si=abc", "YouTubePlaylist", "/youtube/playlist/PL12345", "https://www.youtube.com/feeds/videos.xml?playlist_id=PL12345", "https://www.youtube.com/playlist?list=PL12345")]
    [InlineData("https://www.youtube.com/feeds/videos.xml?playlist_id=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "YouTubePlaylist", "/youtube/playlist/PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "https://www.youtube.com/feeds/videos.xml?playlist_id=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "https://www.youtube.com/playlist?list=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp")]
    [InlineData("youtube://playlist/PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "YouTubePlaylist", "/youtube/playlist/PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "https://www.youtube.com/feeds/videos.xml?playlist_id=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp", "https://www.youtube.com/playlist?list=PLAL-dC6xUrO79IQOrdIpFvUgoCjY0hPFp")]
    public void TryResolve_YouTubePlaylist_ResolvesExpected(string input, string expectedPlatform, string expectedRoute, string expectedFeedUrl, string expectedWebUrl)
    {
        var success = _resolver.TryResolve(input, out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal(expectedPlatform, match.Platform);
        Assert.Equal(expectedRoute, match.RoutePath);
        Assert.Equal(expectedFeedUrl, match.CanonicalFeedUrl);
        Assert.Equal(expectedWebUrl, match.WebUrl);
    }

    [Fact]
    public void TryResolve_CustomRuleFromOptions_ResolvesSuccessfully()
    {
        var options = new RssHubOptions
        {
            BaseUrl = "http://my-rsshub:1200",
            Routes =
            [
                new RssHubRouteRule
                {
                    Platform = "GitHubIssues",
                    UrlPattern = @"^(?:https?://)?github\.com/(?<owner>[\w-]+)/(?<repo>[\w-]+)/issues/?",
                    RouteTemplate = "/github/issue/{owner}/{repo}",
                    CanonicalScheme = "github://{owner}/{repo}/issues",
                    WebUrlTemplate = "https://github.com/{owner}/{repo}/issues"
                }
            ]
        };

        var customResolver = new RssHubRouteResolver(Options.Create(options));
        var success = customResolver.TryResolve("https://github.com/dotnet/aspnetcore/issues", out var match);

        Assert.True(success);
        Assert.NotNull(match);
        Assert.Equal("GitHubIssues", match.Platform);
        Assert.Equal("/github/issue/dotnet/aspnetcore", match.RoutePath);
        Assert.Equal("github://dotnet/aspnetcore/issues", match.CanonicalFeedUrl);
        Assert.Equal("https://github.com/dotnet/aspnetcore/issues", match.WebUrl);
        Assert.Equal("http://my-rsshub:1200", customResolver.GetBaseUrl());
    }

    [Theory]
    [InlineData("https://example.com/blog/feed.xml")]
    [InlineData("https://www.bbc.com/news")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_UnmatchedOrInvalidUrl_ReturnsFalse(string url)
    {
        var success = _resolver.TryResolve(url, out var match);

        Assert.False(success);
        Assert.Null(match);
    }
}
