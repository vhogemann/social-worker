using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Users;

public static class AccountEndpoint
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account")
            .RequireAuthorization();

        group.MapPatch("/password", async (ChangePasswordRequest req, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            {
                return Results.BadRequest("Current password and new password are required.");
            }

            var userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
            if (user == null) return Results.Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            {
                return Results.BadRequest("Incorrect current password.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync(ct);
            db.RefreshTokens.RemoveRange(tokens);

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPatch("/provider", async (UpdatePreferredProviderRequest req, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
            if (user == null) return Results.Unauthorized();

            if (req.ProviderId.HasValue)
            {
                var providerExists = await db.LlmProviders.AnyAsync(p => p.Id == req.ProviderId.Value && p.IsActive, ct);
                if (!providerExists)
                {
                    return Results.BadRequest("Invalid or inactive provider.");
                }
            }

            user.PreferredProviderId = req.ProviderId;
            user.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    public sealed record UpdatePreferredProviderRequest(Guid? ProviderId);
}
