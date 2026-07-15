using System;
using System.Collections.Generic;
using System.Linq;

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
        return _publishers.FirstOrDefault(p => string.Equals(p.Platform, platform, StringComparison.OrdinalIgnoreCase));
    }
}
