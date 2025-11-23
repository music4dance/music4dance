using System.Collections.Concurrent;

namespace m4d.Services;

public interface IBackgroundTaskQueue
{
    // Enqueues the given task.
    void EnqueueTask(Func<IServiceScopeFactory, CancellationToken, Task> task);

    // Dequeues and returns one task. This method blocks until a task becomes available.
    Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly ConcurrentQueue<Func<IServiceScopeFactory, CancellationToken, Task>> _items = new();

    // Holds the current count of tasks in the queue.
    private readonly SemaphoreSlim _signal = new(0);

    public void EnqueueTask(Func<IServiceScopeFactory, CancellationToken, Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        _items.Enqueue(task);
        _ = _signal.Release();
    }

    public async Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        // Wait for task to become available
        await _signal.WaitAsync(cancellationToken);

        _ = _items.TryDequeue(out var task);
        return task;
    }
}