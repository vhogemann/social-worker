namespace SocialWorker.Api.Features.Providers;

public sealed record PlatformCapabilityDto(
    string Platform,
    bool SupportsReplyTarget
);
