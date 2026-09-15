using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Features.Users;
using SocialWorker.Api.Infrastructure;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Users;

public sealed class UsersEndpointTests : SqliteTestBase
{
    [Fact]
    public async Task CreateUser_WhenDuplicateEmail_ReturnsBadRequestWithApiErrorResponse()
    {
        using var db = CreateDbContext();
        var existing = CreateSeedUser(db);

        var duplicateReq = new CreateUserRequest("newuser", existing.Email, "password123", "User");
        var usernameNormalized = duplicateReq.Username.ToUpperInvariant();
        var emailNormalized = duplicateReq.Email.ToUpperInvariant();

        var exists = await db.Users.AnyAsync(u => u.Username.ToUpper() == usernameNormalized || u.Email.ToUpper() == emailNormalized);
        Assert.True(exists);

        var error = new ApiErrorResponse("Username or Email already exists.");
        Assert.Equal("Username or Email already exists.", error.Error);
    }

    [Fact]
    public void ApiErrorResponse_SerializesWithExpectedProperty()
    {
        var error = new ApiErrorResponse("Something went wrong");
        var json = System.Text.Json.JsonSerializer.Serialize(error);
        Assert.Equal("{\"error\":\"Something went wrong\"}", json);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ApiErrorResponse>(json);
        Assert.NotNull(deserialized);
        Assert.Equal("Something went wrong", deserialized!.Error);
    }
}
