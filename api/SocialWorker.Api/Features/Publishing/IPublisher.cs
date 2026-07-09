using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Publishing;

public interface IPublisher
{
    string Platform { get; }
    Task<PublishResult> PublishAsync(PlatformThread thread, CancellationToken ct = default);
}

public class PublishResult
{
    public bool Success { get; set; }
    public string? RemoteId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AuthUrl { get; set; }
}
