using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Publishing;

public interface IPublisher
{
    string Platform { get; }
    Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default);
}

public class PublishResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AuthUrl { get; set; }
    public List<PublishedPost> Posts { get; set; } = new();
}

public class PublishedPost
{
    public int SegmentIndex { get; set; }
    public string RemoteId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
