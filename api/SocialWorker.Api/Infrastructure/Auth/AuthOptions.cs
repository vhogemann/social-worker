namespace SocialWorker.Api.Infrastructure.Auth;

public sealed class AuthOptions
{
    public string JwtSecret { get; set; } = "";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 7;
    public string AdminPassword { get; set; } = "changeme";
}
