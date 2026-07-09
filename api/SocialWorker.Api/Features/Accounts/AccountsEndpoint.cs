using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Accounts;

public static class AccountsEndpoint
{
    public static void MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Results.Unauthorized();

            var accounts = await db.Accounts
                .Where(a => a.UserId == userId)
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
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Results.Unauthorized();

            var encryptionKey = config["Auth:DbEncryptionKey"];
            if (string.IsNullOrEmpty(encryptionKey))
            {
                return Results.Problem("Server encryption key not configured.");
            }

            var existing = await db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == req.Platform);

            if (existing != null)
            {
                existing.Handle = req.Handle;
                existing.CredentialsEncrypted = CryptoHelper.EncryptString(req.AppPassword, encryptionKey);
                existing.UpdatedAt = DateTime.UtcNow;
                existing.Status = "Active";
            }
            else
            {
                var account = new Account
                {
                    UserId = userId,
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
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Results.Unauthorized();

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (account != null)
            {
                db.Accounts.Remove(account);
                await db.SaveChangesAsync();
            }

            return Results.Ok();
        });
    }
}

public record AccountRequest(string Platform, string Handle, string AppPassword);
