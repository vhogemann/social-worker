namespace SocialWorker.Api.Features.Sources.Instagram;

public sealed class InstagramOptions
{
    public const string SectionName = "Instagram";

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "instagram_basic,pages_show_list";
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v20.0/";
    public string OAuthBaseUrl { get; set; } = "https://www.facebook.com/v20.0/dialog/oauth";
    public string? AccessToken { get; set; }
    public string? CallerUserId { get; set; }
}
