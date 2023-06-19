using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Realms.LFS;

internal class Executor<T>
{
    private readonly ConcurrentQueue<T> _queue;
    private readonly Func<T, Task> _onDequeue;
    private readonly Action<Executor<T>> _onComplete;

    public Executor(ConcurrentQueue<T> queue, Func<T, Task> onDequeue, Action<Executor<T>> onComplete)
    {
        _queue = queue;
        _onDequeue = onDequeue;
        _onComplete = onComplete;

        Task.Run(ProcessQueue);
    }

    private async Task ProcessQueue()
    {
        while (_queue.TryDequeue(out var item))
        {
            await _onDequeue(item);
        }

        _onComplete(this);
    }
}