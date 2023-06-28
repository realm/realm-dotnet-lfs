using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Realms.LFS;

internal class ExecutorList<T>
{
    private readonly int _maxSize;
    private readonly object _executorLock = new();
    private readonly List<Executor<T>> _list = new();
    private readonly ConcurrentQueue<T> _queue;
    private readonly Func<T, Task> _executorAction;
    
    private TaskCompletionSource<object?>? _completionTcs;

    public ExecutorList(int maxSize, ConcurrentQueue<T> queue, Func<T, Task> action)
    {
        _maxSize = maxSize;
        _queue = queue;
        _executorAction = action;
    }
    
    public void AddIfNecessary()
    {
        lock (_executorLock)
        {
            if (_queue.Count <= 2 * _list.Count || _list.Count >= _maxSize)
            {
                return;
            }
            
            var executor = new Executor<T>(_queue, _executorAction, Remove);
            _list.Add(executor);

            if (_list.Count == 1)
            {
                _completionTcs = new TaskCompletionSource<object?>();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_executorLock)
            {
                return _list.Count;
            }
        }
    }

    private void Remove(Executor<T> executor)
    {
        lock (_executorLock)
        {
            _list.Remove(executor);

            if (_list.Count == 0)
            {
                _completionTcs?.TrySetResult(null);
            }
        }
    }

    public Task WaitForCompletion() => _completionTcs?.Task ?? Task.CompletedTask;
}