using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SocialWorker.Api.Infrastructure.Background;

public sealed class BackgroundJobQueue
{
    private readonly Channel<Job> _channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(64)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public void Enqueue(Job job)
    {
        _channel.Writer.TryWrite(job);
    }

    public ValueTask<Job> ReadAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAsync(ct);
    }

    public record Job(string Name, Func<CancellationToken, Task> Work);
}