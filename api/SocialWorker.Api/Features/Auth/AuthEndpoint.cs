using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Auth;

public static class AuthEndpoint
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest req, AuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(req.EmailOrUsername, req.Password, ct);
            if (result == null)
            {
                return Results.Json(new { error = "Invalid credentials" }, statusCode: 401);
            }
            return Results.Ok(result);
        });

        group.MapPost("/refresh", async (RefreshRequest req, AuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RefreshAsync(req.RefreshToken, ct);
            if (result == null)
            {
                return Results.Json(new { error = "Invalid or expired refresh token" }, statusCode: 401);
            }
            return Results.Ok(result);
        });

        group.MapPost("/logout", async (LogoutRequest req, AuthService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(req.RefreshToken, ct);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new UserDto(user.Id, user.Username, user.Email, user.Role, user.PreferredProviderId));
        }).RequireAuthorization();
    }
}
