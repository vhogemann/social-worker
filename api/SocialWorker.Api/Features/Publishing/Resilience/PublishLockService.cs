using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Publishing.Resilience;

public interface IPublishLockService
{
    Task<IDisposable?> TryAcquireLockAsync(Guid threadId, TimeSpan timeout, CancellationToken ct = default);
}

public sealed class PublishLockService : IPublishLockService
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable?> TryAcquireLockAsync(Guid threadId, TimeSpan timeout, CancellationToken ct = default)
    {
        var semaphore = _locks.GetOrAdd(threadId, _ => new SemaphoreSlim(1, 1));
        bool entered;
        try
        {
            entered = await semaphore.WaitAsync(timeout, ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (!entered)
        {
            return null;
        }

        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
