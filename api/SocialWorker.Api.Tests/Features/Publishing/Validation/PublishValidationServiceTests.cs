using System;
using System.Threading.Tasks;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Validation;

public sealed class PublishValidationServiceTests : SqliteTestBase
{
    private static PublishValidationService CreateService(AppDbContext db)
    {
        var canonical = new CanonicalDraftValidator();
        var bluesky = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            new IValidateDraftRule<BlueskySegmentValidation>[] { new BlueskyMaxCharactersRule() });
        var linkedIn = new LinkedInDraftValidator();
        var policy = new PlatformContentPolicy();
        var resolver = new DraftValidatorResolver(canonical, bluesky, linkedIn, policy);
        return new PublishValidationService(resolver, db);
    }

    [Fact]
    public async Task ValidateForPublishAsync_InvalidPlatform_ReturnsError()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = Guid.NewGuid(),
            Platform = "NonExistentPlatform",
            Content = "Hello world"
        };

        var error = await service.ValidateForPublishAsync(thread);
        Assert.NotNull(error);
        Assert.Contains("Invalid platform", error);
    }

    [Fact]
    public async Task ValidateForPublishAsync_ValidBlueskyThread_ReturnsNull()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = Guid.NewGuid(),
            Platform = "Bluesky",
            Content = "Valid short post"
        };

        var error = await service.ValidateForPublishAsync(thread);
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateForPublishAsync_OverlengthBlueskyThread_ReturnsError()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = Guid.NewGuid(),
            Platform = "Bluesky",
            Content = new string('A', 350)
        };

        var error = await service.ValidateForPublishAsync(thread);
        Assert.NotNull(error);
    }
}
