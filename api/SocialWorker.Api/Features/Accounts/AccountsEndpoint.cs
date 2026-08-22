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

        group.MapGet("/linkedin/auth-url", (IOptions<LinkedInOptions> options) =>
        {
            var opts = options.Value;
            if (string.IsNullOrEmpty(opts.ClientId) || string.IsNullOrEmpty(opts.RedirectUri))
            {
                return Results.BadRequest(new { error = "LinkedIn client ID or redirect URI is not configured." });
            }

            var scopes = Uri.EscapeDataString("w_member_social openid profile");
            var redirectUri = Uri.EscapeDataString(opts.RedirectUri);
            var authUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={opts.ClientId}&redirect_uri={redirectUri}&scope={scopes}";

            return Results.Ok(new { authUrl });
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

            var tokenResp = await oauth.ExchangeCodeAsync(req.Code, default);
            var userInfo = await oauth.FetchUserInfoAsync(tokenResp.AccessToken, default);

            var authorUrn = userInfo.Sub.Trim();
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

            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}

public record LinkedInCallbackRequest(string Code);

public record AccountRequest(string Platform, string Handle, string AppPassword);
