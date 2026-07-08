using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Auth;

namespace SocialWorker.Api.Features.Auth;

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly AuthOptions _opts;

    public AuthService(AppDbContext db, IOptions<AuthOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    public async Task<LoginResponse?> LoginAsync(string emailOrUsername, string password, CancellationToken ct)
    {
        var normalized = emailOrUsername.ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            (u.Email.ToUpper() == normalized || u.Username.ToUpper() == normalized) && u.IsActive, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var accessToken = CreateAccessToken(user, out var expiresAt);
        var refreshTokenStr = CreateRefreshTokenString();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenStr,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_opts.RefreshTokenLifetimeDays)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new LoginResponse(
            accessToken,
            refreshTokenStr,
            expiresAt,
            new UserDto(user.Id, user.Username, user.Email, user.Role, user.PreferredProviderId)
        );
    }

    public async Task<RefreshResponse?> RefreshAsync(string tokenStr, CancellationToken ct)
    {
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenStr, ct);

        if (token == null || !token.User.IsActive || token.ExpiresAt < DateTime.UtcNow)
        {
            if (token != null)
            {
                _db.RefreshTokens.Remove(token);
                await _db.SaveChangesAsync(ct);
            }
            return null;
        }

        var accessToken = CreateAccessToken(token.User, out var expiresAt);

        token.LastUsedAt = DateTime.UtcNow;
        token.ExpiresAt = DateTime.UtcNow.AddDays(_opts.RefreshTokenLifetimeDays);

        await _db.SaveChangesAsync(ct);

        return new RefreshResponse(accessToken, expiresAt);
    }

    public async Task<bool> LogoutAsync(string tokenStr, CancellationToken ct)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenStr, ct);
        if (token == null) return false;

        _db.RefreshTokens.Remove(token);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private string CreateAccessToken(AppUser user, out DateTime expiresAt)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_opts.JwtSecret);
        expiresAt = DateTime.UtcNow.AddMinutes(_opts.AccessTokenLifetimeMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string CreateRefreshTokenString()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
