using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Auth;

namespace SocialWorker.Api.Features.Users;

public static class UsersEndpoint
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .RequireAuthorization("Admin");

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var users = await db.Users
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .ToListAsync(ct);
            return Results.Ok(users);
        });

        group.MapPost("/", async (CreateUserRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest("Username, Email, and Password are required.");
            }

            var usernameNormalized = req.Username.ToUpperInvariant();
            var emailNormalized = req.Email.ToUpperInvariant();

            if (await db.Users.AnyAsync(u => u.Username.ToUpper() == usernameNormalized || u.Email.ToUpper() == emailNormalized, ct))
            {
                return Results.BadRequest("Username or Email already exists.");
            }

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Username = req.Username,
                Email = req.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Role = req.Role == "Admin" ? "Admin" : "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/users/{user.Id}", new UserDto(user.Id, user.Username, user.Email, user.Role, user.PreferredProviderId));
        });

        group.MapPatch("/{id:guid}", async (Guid id, UpdateUserRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user == null) return Results.NotFound();

            if (req.Username != null) user.Username = req.Username;
            if (req.Email != null) user.Email = req.Email;
            if (req.Role != null) user.Role = req.Role == "Admin" ? "Admin" : "User";
            if (req.IsActive.HasValue)
            {
                user.IsActive = req.IsActive.Value;
                if (!user.IsActive)
                {
                    var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync(ct);
                    db.RefreshTokens.RemoveRange(tokens);
                }
            }

            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new UserDto(user.Id, user.Username, user.Email, user.Role, user.PreferredProviderId));
        });

        group.MapPost("/{id:guid}/password", async (Guid id, ResetPasswordRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword))
            {
                return Results.BadRequest("New password is required.");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user == null) return Results.NotFound();

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync(ct);
            db.RefreshTokens.RemoveRange(tokens);

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
