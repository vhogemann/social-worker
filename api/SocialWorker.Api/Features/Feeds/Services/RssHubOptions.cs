using System.Collections.Generic;

namespace SocialWorker.Api.Features.Feeds.Services;

public sealed class RssHubRouteRule
{
    public string Platform { get; set; } = string.Empty;
    public string UrlPattern { get; set; } = string.Empty;
    public string RouteTemplate { get; set; } = string.Empty;
    public string CanonicalScheme { get; set; } = string.Empty;
    public string WebUrlTemplate { get; set; } = string.Empty;
}

public sealed class RssHubOptions
{
    public const string SectionName = "RssHub";

    public string BaseUrl { get; set; } = "http://rsshub:1200";
    public int TimeoutSeconds { get; set; } = 15;
    public bool PreferRssHub { get; set; } = true;

    public List<RssHubRouteRule> Routes { get; set; } = GetDefaultRoutes();

    public static List<RssHubRouteRule> GetDefaultRoutes() =>
    [
        new()
        {
            Platform = "Instagram",
            UrlPattern = @"^(?:instagram://|(?:https?://)?(?:www\.)?instagram\.com/)(?<user>[a-zA-Z0-9_.]+)/?",
            RouteTemplate = "/instagram/user/{user}",
            CanonicalScheme = "instagram://{user}",
            WebUrlTemplate = "https://www.instagram.com/{user}"
        },
        new()
        {
            Platform = "Twitter",
            UrlPattern = @"^(?:x://|twitter://|(?:https?://)?(?:www\.)?(?:twitter\.com|x\.com)/)(?<user>[a-zA-Z0-9_]+)/?",
            RouteTemplate = "/x/user/{user}",
            CanonicalScheme = "x://{user}",
            WebUrlTemplate = "https://x.com/{user}"
        },
        new()
        {
            Platform = "Telegram",
            UrlPattern = @"^(?:telegram://|(?:https?://)?(?:t\.me|telegram\.me)/(?:s/)?)(?<channel>[a-zA-Z0-9_]+)/?",
            RouteTemplate = "/telegram/channel/{channel}",
            CanonicalScheme = "telegram://{channel}",
            WebUrlTemplate = "https://t.me/{channel}"
        },
        new()
        {
            Platform = "Reddit",
            UrlPattern = @"^(?:reddit://r/|(?:https?://)?(?:www\.)?reddit\.com/r/)(?<sub>[a-zA-Z0-9_]+)/?",
            RouteTemplate = "/reddit/subreddit/{sub}",
            CanonicalScheme = "reddit://r/{sub}",
            WebUrlTemplate = "https://www.reddit.com/r/{sub}"
        },
        new()
        {
            Platform = "RedditUser",
            UrlPattern = @"^(?:reddit://u/|(?:https?://)?(?:www\.)?reddit\.com/(?:u|user)/)(?<user>[a-zA-Z0-9_]+)/?",
            RouteTemplate = "/reddit/user/{user}",
            CanonicalScheme = "reddit://u/{user}",
            WebUrlTemplate = "https://www.reddit.com/user/{user}"
        },
        new()
        {
            Platform = "TikTok",
            UrlPattern = @"^(?:tiktok://|(?:https?://)?(?:www\.)?tiktok\.com/@)(?<user>[a-zA-Z0-9_.]+)/?",
            RouteTemplate = "/tiktok/user/{user}",
            CanonicalScheme = "tiktok://{user}",
            WebUrlTemplate = "https://www.tiktok.com/@{user}"
        },
        new()
        {
            Platform = "Bluesky",
            UrlPattern = @"^(?:bluesky://|(?:https?://)?bsky\.app/profile/)(?<handle>[a-zA-Z0-9_.:-]+)/?",
            RouteTemplate = "/bsky/profile/{handle}",
            CanonicalScheme = "bluesky://{handle}",
            WebUrlTemplate = "https://bsky.app/profile/{handle}"
        },
        new()
        {
            Platform = "YouTubePlaylist",
            UrlPattern = @"^(?:youtube://playlist/(?<id>[a-zA-Z0-9_-]+)|(?:https?://)?(?:www\.)?youtube\.com/(?:feeds/videos\.xml\?[^#]*\bplaylist_id=(?<id>[a-zA-Z0-9_-]+)|playlist\?[^#]*\blist=(?<id>[a-zA-Z0-9_-]+)))",
            RouteTemplate = "/youtube/playlist/{id}",
            CanonicalScheme = "https://www.youtube.com/feeds/videos.xml?playlist_id={id}",
            WebUrlTemplate = "https://www.youtube.com/playlist?list={id}"
        }
    ];
}
