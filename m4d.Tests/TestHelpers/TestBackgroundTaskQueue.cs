using m4d.Services;
using Microsoft.Extensions.DependencyInjection;

namespace m4d.Tests.TestHelpers;

/// <summary>
/// Test implementation of IBackgroundTaskQueue that captures tasks for verification
/// instead of executing them asynchronously in the background.
/// </summary>
public class TestBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Queue<Func<IServiceScopeFactory, CancellationToken, Task>> _tasks = new();

    /// <summary>
    /// Gets the list of tasks that have been enqueued (for test verification)
    /// </summary>
    public IReadOnlyCollection<Func<IServiceScopeFactory, CancellationToken, Task>> Tasks => _tasks.ToList();

    /// <summary>
    /// Gets the count of enqueued tasks
    /// </summary>
    public int Count => _tasks.Count;

    /// <summary>
    /// Enqueues a task for later execution (captured for test verification)
    /// </summary>
    public void EnqueueTask(Func<IServiceScopeFactory, CancellationToken, Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _tasks.Enqueue(task);
    }

    /// <summary>
    /// Dequeues a task (required by interface, throws in tests)
    /// </summary>
    public Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        if (_tasks.Count == 0)
        {
            throw new InvalidOperationException("No tasks to dequeue");
        }
        return Task.FromResult(_tasks.Dequeue());
    }

    /// <summary>
    /// Executes all queued tasks synchronously (for test verification)
    /// Cannot be used in these tests because we don't have IServiceProvider from DanceMusicTester
    /// </summary>
    public async Task ExecuteAllAsync(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var tasksToExecute = _tasks.ToList();
        _tasks.Clear();

        foreach (var task in tasksToExecute)
        {
            await task(scopeFactory, CancellationToken.None);
        }
    }

    /// <summary>
    /// Clears all queued tasks (for test cleanup)
    /// </summary>
    public void Clear()
    {
        _tasks.Clear();
    }
}
