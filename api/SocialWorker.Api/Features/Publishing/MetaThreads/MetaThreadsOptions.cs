namespace SocialWorker.Api.Features.Publishing.MetaThreads;

public sealed class MetaThreadsOptions
{
    public const string SectionName = "MetaThreads";

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "threads_basic,threads_content_publish,threads_manage_replies,threads_read_replies";
    public string BaseUrl { get; set; } = "https://graph.threads.net/v1.0/";
    public string OAuthBaseUrl { get; set; } = "https://threads.net/oauth/";
    public string? PublicBaseUrl { get; set; }
    public int StatusPollingTimeoutSeconds { get; set; } = 240;
}
