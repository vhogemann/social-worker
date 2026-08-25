using System;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.LinkedIn;

namespace SocialWorker.Api.Features.Publishing.Validation;

public interface IDraftValidatorResolver
{
    IDraftValidator Resolve(SocialPlatform? platform);
}

public sealed class DraftValidatorResolver : IDraftValidatorResolver
{
    private readonly CanonicalDraftValidator _canonicalDraftValidator;
    private readonly BlueskyDraftValidator _blueskyDraftValidator;
    private readonly LinkedInDraftValidator _linkedInDraftValidator;
    private readonly PlatformPolicyDraftValidator _twitterValidator;
    private readonly PlatformPolicyDraftValidator _instagramValidator;
    private readonly PlatformPolicyDraftValidator _facebookValidator;

    public DraftValidatorResolver(
        CanonicalDraftValidator canonicalDraftValidator,
        BlueskyDraftValidator blueskyDraftValidator,
        LinkedInDraftValidator linkedInDraftValidator,
        PlatformContentPolicy platformContentPolicy)
    {
        _canonicalDraftValidator = canonicalDraftValidator;
        _blueskyDraftValidator = blueskyDraftValidator;
        _linkedInDraftValidator = linkedInDraftValidator;
        _twitterValidator = new PlatformPolicyDraftValidator(SocialPlatform.Twitter, platformContentPolicy);
        _instagramValidator = new PlatformPolicyDraftValidator(SocialPlatform.Instagram, platformContentPolicy);
        _facebookValidator = new PlatformPolicyDraftValidator(SocialPlatform.Facebook, platformContentPolicy);
    }

    public IDraftValidator Resolve(SocialPlatform? platform)
    {
        if (!platform.HasValue)
        {
            return _canonicalDraftValidator;
        }

        return platform.Value switch
        {
            SocialPlatform.Bluesky => _blueskyDraftValidator,
            SocialPlatform.LinkedIn => _linkedInDraftValidator,
            SocialPlatform.Twitter => _twitterValidator,
            SocialPlatform.Instagram => _instagramValidator,
            SocialPlatform.Facebook => _facebookValidator,
            _ => _canonicalDraftValidator
        };
    }
}
