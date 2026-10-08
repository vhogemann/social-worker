using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Publishing;

public interface IPublisherResolver
{
    IPublisher? Resolve(string platform);
}

public sealed class PublisherResolver : IPublisherResolver
{
    private readonly IEnumerable<IPublisher> _publishers;

    public PublisherResolver(IEnumerable<IPublisher> publishers)
    {
        _publishers = publishers;
    }

    public IPublisher? Resolve(string platform)
    {
        if (SocialPlatformParser.TryParse(platform, out var targetPlatform))
        {
            var match = _publishers.FirstOrDefault(p =>
                SocialPlatformParser.TryParse(p.Platform, out var pubPlatform) && pubPlatform == targetPlatform);
            if (match != null) return match;
        }

        return _publishers.FirstOrDefault(p => string.Equals(p.Platform, platform, StringComparison.OrdinalIgnoreCase));
    }
}
