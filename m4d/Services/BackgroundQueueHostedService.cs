namespace m4d.Services;

public class BackgroundQueueHostedService(IBackgroundTaskQueue taskQueue, IServiceScopeFactory serviceScopeFactory, ILogger<BackgroundQueueHostedService> logger) : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    private readonly ILogger<BackgroundQueueHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Dequeue and execute tasks until the application is stopped
        while (!stoppingToken.IsCancellationRequested)
        {
            // Get next task
            // This blocks until a task becomes available
            var task = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                // Run task
                await task(_serviceScopeFactory, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during execution of a background task");
            }
        }
    }
}