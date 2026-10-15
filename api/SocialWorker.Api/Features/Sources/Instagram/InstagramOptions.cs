namespace SocialWorker.Api.Features.Sources.Instagram;

public sealed class InstagramOptions
{
    public const string SectionName = "Instagram";

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "instagram_business_basic";
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v20.0/";
    public string InstagramGraphBaseUrl { get; set; } = "https://graph.instagram.com/";
    public string OAuthBaseUrl { get; set; } = "https://api.instagram.com/oauth/authorize";
    public string? AccessToken { get; set; }
    public string? CallerUserId { get; set; }
    public string RssHubBaseUrl { get; set; } = "http://rsshub:1200";
    public bool PreferRssHub { get; set; } = true;
}
