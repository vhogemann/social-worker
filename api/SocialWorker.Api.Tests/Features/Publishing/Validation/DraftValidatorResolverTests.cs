using System;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Validation;

public sealed class DraftValidatorResolverTests
{
    private readonly DraftValidatorResolver _resolver;

    public DraftValidatorResolverTests()
    {
        var canonicalValidator = new CanonicalDraftValidator();
        var blueskyValidator = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            Array.Empty<IValidateDraftRule<BlueskySegmentValidation>>());
        var linkedInValidator = new LinkedInDraftValidator();
        var policy = new PlatformContentPolicy();

        _resolver = new DraftValidatorResolver(
            canonicalValidator,
            blueskyValidator,
            linkedInValidator,
            policy);
    }

    [Fact]
    public void Resolve_NullPlatform_ReturnsCanonicalValidator()
    {
        var validator = _resolver.Resolve(null);
        Assert.IsType<CanonicalDraftValidator>(validator);
    }

    [Fact]
    public void Resolve_Bluesky_ReturnsBlueskyValidator()
    {
        var validator = _resolver.Resolve(SocialPlatform.Bluesky);
        Assert.IsType<BlueskyDraftValidator>(validator);
    }

    [Fact]
    public void Resolve_LinkedIn_ReturnsLinkedInValidator()
    {
        var validator = _resolver.Resolve(SocialPlatform.LinkedIn);
        Assert.IsType<LinkedInDraftValidator>(validator);
    }

    [Fact]
    public void Resolve_Twitter_ReturnsPlatformPolicyValidator()
    {
        var validator = _resolver.Resolve(SocialPlatform.Twitter);
        Assert.IsType<PlatformPolicyDraftValidator>(validator);
    }
}
