using System.Collections.Generic;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public interface IReplyPlatformAdapterResolver
{
    IReplyPlatformAdapter? Resolve(string platform);
    IReadOnlyCollection<string> SupportedPlatforms { get; }
}
