using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;

namespace SocialWorker.Api.Features.ReplyAgents.Adapters;

public sealed class ReplyPlatformAdapterResolver : IReplyPlatformAdapterResolver
{
    private readonly Dictionary<string, IReplyPlatformAdapter> _adapters;

    public IReadOnlyCollection<string> SupportedPlatforms => _adapters.Keys;

    public ReplyPlatformAdapterResolver(IEnumerable<IReplyPlatformAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(
            a => a.Platform,
            a => a,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReplyPlatformAdapter? Resolve(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return null;
        return _adapters.GetValueOrDefault(platform);
    }
}
