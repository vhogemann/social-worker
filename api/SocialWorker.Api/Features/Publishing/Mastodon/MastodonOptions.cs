namespace SocialWorker.Api.Features.Publishing.Mastodon;

public sealed class MastodonOptions
{
    public const string SectionName = "Mastodon";

    public string AppName { get; set; } = "social-worker";
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "read write";
    public int MediaPollingTimeoutSeconds { get; set; } = 120;
    public int MediaPollingIntervalMs { get; set; } = 1000;
}
