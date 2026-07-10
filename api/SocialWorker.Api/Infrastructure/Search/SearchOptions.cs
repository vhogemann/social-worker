namespace SocialWorker.Api.Infrastructure.Search;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public string Provider { get; set; } = "SearXng";
    public string BraveApiKey { get; set; } = "";
    public string SearXngBaseUrl { get; set; } = "http://searxng:8080";
}
