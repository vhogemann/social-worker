using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public record LinkedInAccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope
);

public record LinkedInUserInfoResponse(
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("given_name")] string GivenName,
    [property: JsonPropertyName("family_name")] string FamilyName
);

public record LinkedInAclResponse(
    [property: JsonPropertyName("elements")] List<LinkedInAclElement>? Elements
);

public record LinkedInAclElement(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("organizationalTarget")] string? OrganizationalTarget
);

public record LinkedInOrganizationResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("localizedName")] string? LocalizedName,
    [property: JsonPropertyName("vanityName")] string? VanityName
);

public record LinkedInOrganizationInfo(
    string Urn,
    string Name,
    string? VanityName,
    string Role
);

public sealed class LinkedInOAuthService
{
    private static readonly HashSet<string> ValidPostingRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMINISTRATOR",
        "CONTENT_ADMINISTRATOR",
        "DIRECT_SPONSORED_CONTENT_POSTER",
        "LEAD_GEN_FORMS_MANAGER"
    };

    private readonly HttpClient _httpClient;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInOAuthService>? _logger;

    public LinkedInOAuthService(
        HttpClient httpClient,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInOAuthService>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<LinkedInAccessTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct) =>
        ExchangeCodeAsync(code, isCompany: false, ct);

    public async Task<LinkedInAccessTokenResponse> ExchangeCodeAsync(string code, bool isCompany = false, CancellationToken ct = default)
    {
        var clientId = isCompany && !string.IsNullOrEmpty(_options.CompanyClientId)
            ? _options.CompanyClientId
            : _options.ClientId;

        var clientSecret = isCompany && !string.IsNullOrEmpty(_options.CompanyClientSecret)
            ? _options.CompanyClientSecret
            : _options.ClientSecret;

        var redirectUri = isCompany && !string.IsNullOrEmpty(_options.CompanyRedirectUri)
            ? _options.CompanyRedirectUri
            : _options.RedirectUri;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
        {
            throw new InvalidOperationException($"LinkedIn {(isCompany ? "company " : "")}app credentials are not configured.");
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        });

        using var response = await _httpClient.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", form, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to exchange LinkedIn code: {response.StatusCode} - {errorContent}");
        }

        var data = await response.Content.ReadFromJsonAsync<LinkedInAccessTokenResponse>(cancellationToken: ct);
        return data ?? throw new InvalidOperationException("Empty response from access token endpoint.");
    }

    public async Task<LinkedInUserInfoResponse?> FetchUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogDebug("LinkedIn userinfo returned status {StatusCode}; app may lack openid/profile scope.", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LinkedInUserInfoResponse>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to fetch LinkedIn user info.");
            return null;
        }
    }

    public async Task<IReadOnlyList<LinkedInOrganizationInfo>> FetchOrganizationsAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/organizationalEntityAcls?q=roleAssignee&state=APPROVED");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to retrieve LinkedIn organizational ACLs: {StatusCode}", response.StatusCode);
                return Array.Empty<LinkedInOrganizationInfo>();
            }

            var aclData = await response.Content.ReadFromJsonAsync<LinkedInAclResponse>(cancellationToken: ct);
            if (aclData?.Elements == null || aclData.Elements.Count == 0)
            {
                return Array.Empty<LinkedInOrganizationInfo>();
            }

            var results = new List<LinkedInOrganizationInfo>();
            var seenUrns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in aclData.Elements)
            {
                if (string.IsNullOrWhiteSpace(element.OrganizationalTarget) || string.IsNullOrWhiteSpace(element.Role))
                    continue;

                if (!string.Equals(element.State, "APPROVED", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ValidPostingRoles.Contains(element.Role))
                    continue;

                var targetUrn = element.OrganizationalTarget.Trim();
                if (!seenUrns.Add(targetUrn))
                    continue;

                var orgIdStr = targetUrn.Split(':').LastOrDefault();
                string orgName = targetUrn;
                string? vanityName = null;

                if (!string.IsNullOrEmpty(orgIdStr) && long.TryParse(orgIdStr, out _))
                {
                    var orgDetails = await FetchOrganizationDetailsAsync(orgIdStr, accessToken, ct);
                    if (orgDetails != null)
                    {
                        if (!string.IsNullOrWhiteSpace(orgDetails.LocalizedName))
                        {
                            orgName = orgDetails.LocalizedName;
                        }
                        vanityName = orgDetails.VanityName;
                    }
                }

                results.Add(new LinkedInOrganizationInfo(targetUrn, orgName, vanityName, element.Role));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Exception while fetching LinkedIn organizations.");
            return Array.Empty<LinkedInOrganizationInfo>();
        }
    }

    public async Task<LinkedInOrganizationResponse?> FetchOrganizationDetailsAsync(string orgId, string accessToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.linkedin.com/v2/organizations/{orgId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LinkedInOrganizationResponse>(cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }
}
