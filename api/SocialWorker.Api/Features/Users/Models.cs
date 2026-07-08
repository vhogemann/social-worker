namespace SocialWorker.Api.Features.Users;

public sealed record CreateUserRequest(string Username, string Email, string Password, string Role);
public sealed record UpdateUserRequest(string? Username, string? Email, string? Role, bool? IsActive);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ResetPasswordRequest(string NewPassword);
