namespace SocialWorker.Api.Features.Publishing.Twitter;

public sealed class TwitterOptions
{
    public const string SectionName = "Twitter";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "tweet.read tweet.write users.read offline.access";
    public string BaseUrl { get; set; } = "https://api.twitter.com/2/";
    public string OAuthBaseUrl { get; set; } = "https://twitter.com/i/oauth2/";
    public int InterTweetDelayMs { get; set; } = 1000;
}
