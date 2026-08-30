using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private readonly ApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? apiKeyCandidate = null;

        if (Request.Headers.TryGetValue("X-API-Key", out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
        {
            apiKeyCandidate = headerValue.ToString();
        }
        else if (Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
        {
            var auth = authHeader.ToString().Trim();
            if (auth.StartsWith("Bearer sw_live_", StringComparison.OrdinalIgnoreCase))
            {
                apiKeyCandidate = auth.Substring(7).Trim();
            }
            else if (auth.StartsWith("sw_live_", StringComparison.OrdinalIgnoreCase))
            {
                apiKeyCandidate = auth;
            }
        }
        else if (Request.Query.TryGetValue("api_key", out var queryValue) && !string.IsNullOrWhiteSpace(queryValue))
        {
            apiKeyCandidate = queryValue.ToString();
        }

        if (string.IsNullOrWhiteSpace(apiKeyCandidate))
        {
            return AuthenticateResult.NoResult();
        }

        var key = await _apiKeyService.ValidateApiKeyAsync(apiKeyCandidate, Context.RequestAborted);
        if (key == null || key.User == null)
        {
            return AuthenticateResult.Fail("Invalid, expired, or revoked API key.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.UserId.ToString()),
            new(ClaimTypes.Name, key.User.Username),
            new(ClaimTypes.Role, key.User.Role ?? "User"),
            new("api_key_id", key.Id.ToString()),
            new("scopes", key.Scopes),
            new("auth_method", "api_key")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
