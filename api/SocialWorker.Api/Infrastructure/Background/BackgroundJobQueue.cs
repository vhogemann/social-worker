using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SocialWorker.Api.Infrastructure.Background;

public sealed class BackgroundJobQueue
{
    private readonly Channel<Job> _channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(64)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundJobQueue(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Enqueue(Job job)
    {
        _channel.Writer.TryWrite(job);
    }

    public void EnqueueScoped(string name, Func<IServiceProvider, CancellationToken, Task> work)
    {
        Enqueue(new Job(name, async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            await work(scope.ServiceProvider, ct);
        }));
    }

    public ValueTask<Job> ReadAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAsync(ct);
    }

    public record Job(string Name, Func<CancellationToken, Task> Work);
}