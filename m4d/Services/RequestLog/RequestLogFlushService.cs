namespace m4d.Services.RequestLog;

/// <summary>
/// Every FlushIntervalSeconds, writes the queued requests to their hourly blobs, and once more
/// on shutdown. A batch that fails to write is dropped, not retried, so an outage costs log
/// lines rather than memory. Failures are logged at most every 15 minutes.
/// </summary>
public class RequestLogFlushService(
    RequestLogQueue queue,
    IRequestLogSink sink,
    RequestLogOptions options,
    ILogger<RequestLogFlushService> logger) : BackgroundService
{
    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(15);

    private readonly string _prefix = options.EffectivePrefix;
    private DateTime _lastWarning = DateTime.MinValue;
    private long _unreportedDropped;
    private Exception _unreportedFailure;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Request log: writing to {Uri}/{Container}/{Prefix} every {Seconds}s",
            options.ServiceUri, options.Container, _prefix, options.FlushIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.FlushIntervalSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }

        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await FlushAsync(shutdown.Token);
    }

    /// <summary>Writes what's queued. Returns the number of requests written.</summary>
    internal async Task<int> FlushAsync(CancellationToken cancellationToken)
    {
        var dropped = queue.TakeDropped();
        var entries = queue.Drain();
        var written = 0;

        foreach (var hour in entries.GroupBy(e => RequestLogEntry.BlobName(_prefix, e.Time)))
        {
            var batch = hour.ToList();
            try
            {
                await sink.AppendAsync(hour.Key, RequestLogEntry.ToBlocks(batch), cancellationToken);
                written += batch.Count;
            }
            catch (Exception ex)
            {
                _unreportedFailure = ex;
                dropped += batch.Count;
            }
        }

        _unreportedDropped += dropped;
        if (_unreportedDropped > 0 && DateTime.UtcNow - _lastWarning >= WarningInterval)
        {
            if (_unreportedFailure != null)
            {
                logger.LogWarning(_unreportedFailure, "Request log: dropped {Count} requests since the last report; a write failed",
                    _unreportedDropped);
            }
            else
            {
                logger.LogWarning("Request log: dropped {Count} requests since the last report (queue full)",
                    _unreportedDropped);
            }
            _lastWarning = DateTime.UtcNow;
            _unreportedDropped = 0;
            _unreportedFailure = null;
        }

        return written;
    }
}
