namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public sealed class LinkedInOptions
{
    public const string SectionName = "LinkedIn";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "w_member_social openid profile";

    // Dedicated Company App (Community Management API)
    public string CompanyClientId { get; set; } = string.Empty;
    public string CompanyClientSecret { get; set; } = string.Empty;
    public string CompanyRedirectUri { get; set; } = string.Empty;
    public string CompanyScopes { get; set; } = "w_organization_social r_organization_admin";
}
