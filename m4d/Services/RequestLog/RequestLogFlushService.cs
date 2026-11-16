namespace m4d.Services.RequestLog;

/// <summary>
/// Every FlushIntervalSeconds, writes the queued requests to their hourly blobs, and once more
/// on shutdown. A batch that fails to write is dropped, not retried, so an outage costs log
/// lines rather than memory. Failures are logged at most every 15 minutes.
///
/// Writes aren't tied to the host's stopping token: a shutdown that lands mid-append lets the
/// append finish (each flush has its own timeout) instead of cancelling it and dropping that
/// batch, and the final flush then picks up whatever arrived since.
/// </summary>
public class RequestLogFlushService(
    RequestLogQueue queue,
    IRequestLogSink sink,
    RequestLogOptions options,
    ILogger<RequestLogFlushService> logger) : BackgroundService
{
    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(10);

    private readonly string _prefix = options.EffectivePrefix;
    private DateTime _lastWarning = DateTime.MinValue;
    private long _unreportedDropped;
    private Exception _unreportedFailure;

    // Reused by every flush, so batches don't allocate block copies. The gate keeps the final
    // flush from overlapping a periodic one that outlived the host's stop timeout.
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private byte[] _block;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Request log: writing to {Uri}/{Container}/{Prefix} every {Seconds}s",
            options.ServiceUri, options.Container, _prefix, options.FlushIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.FlushIntervalSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var timeout = new CancellationTokenSource(FlushTimeout);
                await FlushAsync(timeout.Token);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Stops the periodic loop (letting an in-flight flush finish), then flushes once more. Done
    /// here rather than at the end of ExecuteAsync, which never runs if the host stops before
    /// the loop has been scheduled.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        using var shutdown = new CancellationTokenSource(ShutdownFlushTimeout);
        await FlushAsync(shutdown.Token);
    }

    /// <summary>Writes what's queued. Returns the number of requests written.</summary>
    internal async Task<int> FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _flushGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }

        try
        {
            return await FlushLockedAsync(cancellationToken);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private async Task<int> FlushLockedAsync(CancellationToken cancellationToken)
    {
        var dropped = queue.TakeDropped();
        var entries = queue.Drain();
        var written = 0;

        if (entries.Count > 0)
        {
            _block ??= new byte[RequestLogEntry.MaxBlockBytes];
        }

        foreach (var hour in entries.GroupBy(e => RequestLogEntry.BlobName(_prefix, e.Time)))
        {
            var batch = hour.ToList();
            try
            {
                written += await RequestLogEntry.WriteBlocksAsync(batch, _block,
                    (buffer, count) => sink.AppendAsync(hour.Key, buffer, count, cancellationToken));
            }
            catch (Exception ex)
            {
                // Some of the batch may already be in the blob; count it all as dropped.
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
