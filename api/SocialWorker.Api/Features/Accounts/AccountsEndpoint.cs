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
using SocialWorker.Api.Infrastructure.Security;
using SocialWorker.Api.Infrastructure;

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
                    a.Status,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(accounts);
        });

        group.MapPost("/", async (AccountRequest req, AppDbContext db, ClaimsPrincipal user, IConfiguration config) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var existing = await db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId.Value && a.Platform == req.Platform && a.Handle == req.Handle);

            if (existing != null)
            {
                existing.CredentialsEncrypted = CryptoHelper.EncryptString(req.AppPassword, encryptionKey);
                existing.UpdatedAt = DateTime.UtcNow;
                existing.Status = "Active";
            }
            else
            {
                var account = new Account
                {
                    UserId = userId.Value,
                    Platform = req.Platform,
                    Handle = req.Handle,
                    CredentialsEncrypted = CryptoHelper.EncryptString(req.AppPassword, encryptionKey),
                    Status = "Active"
                };
                db.Accounts.Add(account);
            }

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

        group.MapGet("/linkedin/auth-url", (string? type, IOptions<LinkedInOptions> options) =>
        {
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
            var state = isCompany ? "company" : "personal";
            var authUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={clientId}&redirect_uri={redirectUriEscaped}&scope={scopes}&state={state}";

            return Results.Ok(new { authUrl, accountType = state });
        });

        group.MapPost("/linkedin/callback", async (LinkedInCallbackRequest req, LinkedInOAuthService oauth, AppDbContext db, ClaimsPrincipal user, IConfiguration config) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var isCompany = string.Equals(req.AccountType, "company", StringComparison.OrdinalIgnoreCase);
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

                var existing = await db.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == userId.Value && a.Platform == "LinkedIn" && a.Handle == authorUrn);

                if (existing != null)
                {
                    existing.CredentialsEncrypted = CryptoHelper.EncryptString(tokenResp.AccessToken, encryptionKey);
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Status = "Active";
                }
                else
                {
                    var account = new Account
                    {
                        UserId = userId.Value,
                        Platform = "LinkedIn",
                        Handle = authorUrn,
                        CredentialsEncrypted = CryptoHelper.EncryptString(tokenResp.AccessToken, encryptionKey),
                        Status = "Active"
                    };
                    db.Accounts.Add(account);
                }
            }

            var connectedOrgHandles = new List<string>();
            var orgs = await oauth.FetchOrganizationsAsync(tokenResp.AccessToken, default);
            foreach (var org in orgs)
            {
                var orgHandle = !string.IsNullOrWhiteSpace(org.Name)
                    ? $"{org.Name} ({org.Urn})"
                    : org.Urn;

                var orgExisting = await db.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == userId.Value && a.Platform == "LinkedIn" 
                        && (a.Handle == orgHandle || a.Handle == org.Urn));

                if (orgExisting != null)
                {
                    orgExisting.Handle = orgHandle;
                    orgExisting.CredentialsEncrypted = CryptoHelper.EncryptString(tokenResp.AccessToken, encryptionKey);
                    orgExisting.UpdatedAt = DateTime.UtcNow;
                    orgExisting.Status = "Active";
                }
                else
                {
                    var orgAccount = new Account
                    {
                        UserId = userId.Value,
                        Platform = "LinkedIn",
                        Handle = orgHandle,
                        CredentialsEncrypted = CryptoHelper.EncryptString(tokenResp.AccessToken, encryptionKey),
                        Status = "Active"
                    };
                    db.Accounts.Add(orgAccount);
                }

                connectedOrgHandles.Add(orgHandle);
            }

            if (authorUrn is null && connectedOrgHandles.Count == 0)
            {
                return Results.BadRequest(new ApiErrorResponse("No LinkedIn personal profile or company pages could be connected with this authorization."));
            }

            await db.SaveChangesAsync();
            return Results.Ok(new LinkedInCallbackResponse(authorUrn, connectedOrgHandles));
        });
    }
}

public record LinkedInCallbackRequest(string Code, string? AccountType = null);
public record LinkedInCallbackResponse(string? PersonalHandle, IReadOnlyList<string> OrganizationHandles);

public record AccountRequest(string Platform, string Handle, string AppPassword);
