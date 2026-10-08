using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Infrastructure.Security;
using SocialWorker.Api.Infrastructure;
using System.Text.Json;

namespace SocialWorker.Api.Features.Accounts;

public static class AccountsEndpoint
{
    public static void MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var accounts = await db.Accounts
                .Where(a => a.UserId == userId.Value)
                .Select(a => new
                {
                    a.Id,
                    a.Platform,
                    a.Handle,
                    a.ExternalId,
                    a.TokenExpiresAt,
                    a.Status,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(accounts);
        });

        group.MapPost("/", async (AccountRequest req, AccountUpsertService upsertService, AppDbContext db, ClaimsPrincipal user, IConfiguration config) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var encryptedPassword = CryptoHelper.EncryptString(req.AppPassword, encryptionKey);
            await upsertService.UpsertAsync(userId.Value, req.Platform, req.Handle, encryptedPassword);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);
            if (account != null)
            {
                db.Accounts.Remove(account);
                await db.SaveChangesAsync();
            }

            return Results.Ok();
        });

        group.MapGet("/linkedin/auth-url", async (string? type, IOptions<LinkedInOptions> options, OAuthStateService oauthStateService, ClaimsPrincipal user) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var opts = options.Value;
            var isCompany = string.Equals(type, "company", StringComparison.OrdinalIgnoreCase);

            var clientId = isCompany && !string.IsNullOrWhiteSpace(opts.CompanyClientId)
                ? opts.CompanyClientId
                : opts.ClientId;

            var redirectUri = isCompany && !string.IsNullOrWhiteSpace(opts.CompanyRedirectUri)
                ? opts.CompanyRedirectUri
                : opts.RedirectUri;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
            {
                var targetType = isCompany ? "company " : "";
                return Results.BadRequest(new ApiErrorResponse($"LinkedIn {targetType}client ID or redirect URI is not configured."));
            }

            var defaultScopes = isCompany
                ? (!string.IsNullOrWhiteSpace(opts.CompanyScopes) ? opts.CompanyScopes : "w_organization_social r_organization_admin")
                : (!string.IsNullOrWhiteSpace(opts.Scopes) ? opts.Scopes : "w_member_social openid profile");

            var scopes = Uri.EscapeDataString(defaultScopes);
            var redirectUriEscaped = Uri.EscapeDataString(redirectUri);
            var accountType = isCompany ? "company" : "personal";

            var statePayload = JsonSerializer.Serialize(new { AccountType = accountType });
            var stateNonce = await oauthStateService.IssueAsync(userId.Value, "LinkedIn", statePayload);

            var authUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={clientId}&redirect_uri={redirectUriEscaped}&scope={scopes}&state={stateNonce}";

            return Results.Ok(new { authUrl, accountType, state = stateNonce });
        });

        group.MapPost("/linkedin/callback", async (LinkedInCallbackRequest req, LinkedInOAuthService oauth, OAuthStateService oauthStateService, AccountUpsertService upsertService, AppDbContext db, ClaimsPrincipal user, IConfiguration config) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var stateNonce = req.State;
            string? targetAccountType = req.AccountType;

            if (!string.IsNullOrWhiteSpace(stateNonce))
            {
                var oauthState = await oauthStateService.ConsumeAsync(userId.Value, "LinkedIn", stateNonce);
                if (oauthState == null)
                {
                    return Results.BadRequest(new ApiErrorResponse("Invalid or expired OAuth state. Please try connecting again."));
                }

                if (!string.IsNullOrWhiteSpace(oauthState.PayloadJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(oauthState.PayloadJson);
                        if (doc.RootElement.TryGetProperty("AccountType", out var prop))
                        {
                            targetAccountType = prop.GetString();
                        }
                    }
                    catch
                    {
                    }
                }
            }

            var isCompany = string.Equals(targetAccountType, "company", StringComparison.OrdinalIgnoreCase);
            var tokenResp = await oauth.ExchangeCodeAsync(req.Code, isCompany, default);
            var userInfo = await oauth.FetchUserInfoAsync(tokenResp.AccessToken, default);

            string? authorUrn = null;
            if (userInfo != null && !string.IsNullOrWhiteSpace(userInfo.Sub))
            {
                authorUrn = userInfo.Sub.Trim();
                if (!authorUrn.StartsWith("urn:li:", StringComparison.OrdinalIgnoreCase))
                {
                    authorUrn = $"urn:li:person:{authorUrn}";
                }

                var encryptedToken = CryptoHelper.EncryptString(tokenResp.AccessToken, encryptionKey);
                await upsertService.UpsertAsync(userId.Value, "LinkedIn", authorUrn, encryptedToken);
            }

            var connectedOrgHandles = new List<string>();
            var orgs = await oauth.FetchOrganizationsAsync(tokenResp.AccessToken, default);
            foreach (var org in orgs)
            {
                var orgHandle = !string.IsNullOrWhiteSpace(org.Name)
                    ? $"{org.Name} ({org.Urn})"
                    : org.Urn;

                var encryptedToken = CryptoHelper.EncryptString(tokenResp.AccessToken, encryptionKey);
                await upsertService.UpsertAsync(userId.Value, "LinkedIn", orgHandle, encryptedToken);
                connectedOrgHandles.Add(orgHandle);
            }

            if (authorUrn is null && connectedOrgHandles.Count == 0)
            {
                return Results.BadRequest(new ApiErrorResponse("No LinkedIn personal profile or company pages could be connected with this authorization."));
            }

            await db.SaveChangesAsync();
            return Results.Ok(new LinkedInCallbackResponse(authorUrn, connectedOrgHandles));
        });

        group.MapGet("/metathreads/auth-url", async (
            ClaimsPrincipal user,
            OAuthStateService oauthStateService,
            MetaThreadsOAuthService threadsOAuth) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var nonce = await oauthStateService.IssueAsync(userId.Value, "metathreads");
            var authUrl = threadsOAuth.GetAuthorizationUrl(nonce);
            return Results.Ok(new { authUrl });
        });

        group.MapPost("/metathreads/callback", async (
            MetaThreadsCallbackRequest req,
            ClaimsPrincipal user,
            OAuthStateService oauthStateService,
            AccountUpsertService upsertService,
            MetaThreadsOAuthService threadsOAuth,
            AppDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return Results.BadRequest(new ApiErrorResponse("Authorization code is required."));
            }

            if (string.IsNullOrWhiteSpace(req.State))
            {
                return Results.BadRequest(new ApiErrorResponse("OAuth state parameter is required."));
            }

            var oauthState = await oauthStateService.ConsumeAsync(userId.Value, "metathreads", req.State, ct);
            if (oauthState is null)
            {
                return Results.BadRequest(new ApiErrorResponse("Invalid or expired OAuth state parameter."));
            }

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var shortToken = await threadsOAuth.ExchangeCodeAsync(req.Code, ct);
            var longToken = await threadsOAuth.ExchangeForLongLivedTokenAsync(shortToken.AccessToken, ct);
            var profile = await threadsOAuth.FetchUserProfileAsync(longToken.AccessToken, ct);

            var expiresAt = DateTime.UtcNow.AddSeconds(longToken.ExpiresIn);
            var encryptedToken = CryptoHelper.EncryptString(longToken.AccessToken, encryptionKey);

            await upsertService.UpsertAsync(
                userId: userId.Value,
                platform: "MetaThreads",
                handle: profile.Username,
                credentialsEncrypted: encryptedToken,
                externalId: profile.Id,
                tokenExpiresAt: expiresAt,
                ct: ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(new MetaThreadsCallbackResponse(profile.Id, profile.Username, expiresAt));
        });

        group.MapGet("/twitter/auth-url", async (
            ClaimsPrincipal user,
            OAuthStateService oauthStateService,
            TwitterOAuthService twitterOAuth) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var verifier = TwitterOAuthService.GenerateCodeVerifier();
            var challenge = TwitterOAuthService.GenerateCodeChallenge(verifier);
            var payloadJson = JsonSerializer.Serialize(new TwitterPkceState(verifier));

            var nonce = await oauthStateService.IssueAsync(userId.Value, "twitter", payloadJson);
            var authUrl = twitterOAuth.GetAuthorizationUrl(nonce, challenge);
            return Results.Ok(new { authUrl });
        });

        group.MapPost("/twitter/callback", async (
            TwitterCallbackRequest req,
            ClaimsPrincipal user,
            OAuthStateService oauthStateService,
            AccountUpsertService upsertService,
            TwitterOAuthService twitterOAuth,
            AppDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return Results.BadRequest(new ApiErrorResponse("Authorization code is required."));
            }

            if (string.IsNullOrWhiteSpace(req.State))
            {
                return Results.BadRequest(new ApiErrorResponse("OAuth state parameter is required."));
            }

            var oauthState = await oauthStateService.ConsumeAsync(userId.Value, "twitter", req.State, ct);
            if (oauthState is null)
            {
                return Results.BadRequest(new ApiErrorResponse("Invalid or expired OAuth state parameter."));
            }

            string codeVerifier;
            try
            {
                var pkce = JsonSerializer.Deserialize<TwitterPkceState>(oauthState.PayloadJson ?? "");
                codeVerifier = pkce?.CodeVerifier ?? "";
            }
            catch
            {
                codeVerifier = "";
            }

            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                return Results.BadRequest(new ApiErrorResponse("Missing PKCE code verifier in state."));
            }

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var tokenResp = await twitterOAuth.ExchangeCodeAsync(req.Code, codeVerifier, ct);
            var profile = await twitterOAuth.FetchUserProfileAsync(tokenResp.AccessToken, ct);

            var credentials = new TwitterAccountCredentials(
                AccessToken: tokenResp.AccessToken,
                RefreshToken: tokenResp.RefreshToken,
                TokenType: tokenResp.TokenType,
                Scope: tokenResp.Scope);

            var credentialsJson = JsonSerializer.Serialize(credentials);
            var encryptedToken = CryptoHelper.EncryptString(credentialsJson, encryptionKey);
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResp.ExpiresIn);

            await upsertService.UpsertAsync(
                userId: userId.Value,
                platform: "Twitter",
                handle: profile.Username,
                credentialsEncrypted: encryptedToken,
                externalId: profile.Id,
                tokenExpiresAt: expiresAt,
                ct: ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(new TwitterCallbackResponse(profile.Id, profile.Username, expiresAt));
        });
    }
}

public record LinkedInCallbackRequest(string Code, string? State = null, string? AccountType = null);
public record LinkedInCallbackResponse(string? PersonalHandle, IReadOnlyList<string> OrganizationHandles);

public record MetaThreadsCallbackRequest(string Code, string State);
public record MetaThreadsCallbackResponse(string ExternalId, string Username, DateTime TokenExpiresAt);

public record TwitterCallbackRequest(string Code, string State);
public record TwitterCallbackResponse(string ExternalId, string Username, DateTime TokenExpiresAt);

public record AccountRequest(string Platform, string Handle, string AppPassword);
