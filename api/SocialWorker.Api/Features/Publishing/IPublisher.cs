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
    public bool IsPartial { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AuthUrl { get; set; }
    public List<PublishedPost> Posts { get; set; } = new();

    public static PublishResult Succeeded(IEnumerable<PublishedPost> posts) =>
        new() { Success = true, Posts = posts is List<PublishedPost> list ? list : new List<PublishedPost>(posts) };

    public static PublishResult Partial(IEnumerable<PublishedPost> posts, string error) =>
        new() { Success = false, IsPartial = true, ErrorMessage = error, Posts = posts is List<PublishedPost> list ? list : new List<PublishedPost>(posts) };

    public static PublishResult Failed(string error, string? authUrl = null, IEnumerable<PublishedPost>? posts = null) =>
        new() { Success = false, ErrorMessage = error, AuthUrl = authUrl, Posts = posts != null ? new List<PublishedPost>(posts) : new() };
}

public class PublishedPost
{
    public int SegmentIndex { get; set; }
    public string RemoteId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? RemoteCid { get; set; }
    public string? PublishedText { get; set; }
}
