using System;

namespace SocialWorker.Api.Features.Auth;

public sealed record LoginRequest(string EmailOrUsername, string Password);
public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);
public sealed record RefreshRequest(string RefreshToken);
public sealed record RefreshResponse(string AccessToken, DateTime ExpiresAt);
public sealed record LogoutRequest(string RefreshToken);
public sealed record UserDto(Guid Id, string Username, string Email, string Role, Guid? PreferredProviderId);
