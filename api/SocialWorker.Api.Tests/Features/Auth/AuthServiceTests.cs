using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Infrastructure.Auth;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class AuthServiceTests : SqliteTestBase
{
    private AuthService CreateService(AppDbContext db, string jwtSecret = "test_secret_key_that_is_at_least_32_chars_long_for_security")
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new AuthOptions
        {
            JwtSecret = jwtSecret,
            AccessTokenLifetimeMinutes = 60,
            RefreshTokenLifetimeDays = 7,
            AdminPassword = "changeme"
        });
        return new AuthService(db, opts);
    }

    private static async Task<AppUser> SeedUserAsync(AppDbContext db, string username = "testuser", string password = "password123", bool isActive = true)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "User",
            IsActive = isActive
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var user = await SeedUserAsync(db);
        var svc = CreateService(db);

        var result = await svc.LoginAsync("testuser", "password123", CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("testuser", result.User.Username);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        await SeedUserAsync(db);
        var svc = CreateService(db);

        var result = await svc.LoginAsync("testuser", "wrongpassword", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUsername_ReturnsNull()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var svc = CreateService(db);

        var result = await svc.LoginAsync("nonexistent", "password123", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsNull()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        await SeedUserAsync(db, "inactive", "password123", isActive: false);
        var svc = CreateService(db);

        var result = await svc.LoginAsync("inactive", "password123", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_CanLoginWithEmail()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        await SeedUserAsync(db, "testuser", "password123");
        var svc = CreateService(db);

        var result = await svc.LoginAsync("testuser@example.com", "password123", CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_ReturnsNewAccessToken()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var user = await SeedUserAsync(db);
        var svc = CreateService(db);

        // Login first to get a refresh token
        var loginResult = await svc.LoginAsync("testuser", "password123", CancellationToken.None);
        Assert.NotNull(loginResult);

        var refreshResult = await svc.RefreshAsync(loginResult.RefreshToken, CancellationToken.None);

Assert.NotNull(refreshResult);
    Assert.NotNull(refreshResult.AccessToken);
    // Refresh token expiry time should be set
    Assert.True(refreshResult.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidToken_ReturnsNull()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var svc = CreateService(db);

        var result = await svc.RefreshAsync("nonexistent-token", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshAsync_WithRevokedToken_ReturnsNull()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        await SeedUserAsync(db);
        var svc = CreateService(db);

        var loginResult = await svc.LoginAsync("testuser", "password123", CancellationToken.None);
        Assert.NotNull(loginResult);

        // Logout (revoke the token)
        await svc.LogoutAsync(loginResult.RefreshToken, CancellationToken.None);

        // Refresh should now fail
        var refreshResult = await svc.RefreshAsync(loginResult.RefreshToken, CancellationToken.None);
        Assert.Null(refreshResult);
    }

    [Fact]
    public async Task LogoutAsync_WithValidToken_RemovesToken()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        await SeedUserAsync(db);
        var svc = CreateService(db);

        var loginResult = await svc.LoginAsync("testuser", "password123", CancellationToken.None);
        Assert.NotNull(loginResult);

        var loggedOut = await svc.LogoutAsync(loginResult.RefreshToken, CancellationToken.None);
        Assert.True(loggedOut);

        // Token should no longer exist
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == loginResult.RefreshToken);
        Assert.Null(token);
    }

    [Fact]
    public async Task LogoutAsync_WithInvalidToken_ReturnsFalse()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        var svc = CreateService(db);

        var result = await svc.LogoutAsync("nonexistent-token", CancellationToken.None);

        Assert.False(result);
    }
}