namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public sealed class LinkedInOptions
{
    public const string SectionName = "LinkedIn";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
