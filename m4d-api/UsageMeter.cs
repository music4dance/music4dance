using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace m4dApi;

/// <summary>The usage table, behind an interface so tests can fake it.</summary>
public interface IUsageTable
{
    /// <summary>
    /// Adds <paramref name="counts"/> to the client's row for <paramref name="day"/> and returns
    /// the row's new total, which leaves out the limited count. With no counts it only reads the
    /// total.
    /// </summary>
    Task<long> Add(string clientId, string day, IReadOnlyDictionary<string, long> counts,
        CancellationToken cancellationToken);
}

/// <summary>
/// One row per client and UTC day (PartitionKey client id, RowKey yyyy-MM-dd), with a count per
/// result and a total. Instances update rows with optimistic concurrency, so counts from several
/// instances add up.
/// </summary>
public class StorageUsageTable(TableClient table) : IUsageTable
{
    public const string TotalColumn = "total";
    private const int Attempts = 5;
    private bool _created;

    public async Task<long> Add(string clientId, string day, IReadOnlyDictionary<string, long> counts,
        CancellationToken cancellationToken)
    {
        if (!_created)
        {
            await table.CreateIfNotExistsAsync(cancellationToken);
            _created = true;
        }

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                var existing = await table.GetEntityIfExistsAsync<TableEntity>(
                    clientId, day, cancellationToken: cancellationToken);
                var entity = existing.HasValue ? existing.Value : new TableEntity(clientId, day);
                if (counts.Count == 0)
                {
                    return entity.GetInt64(TotalColumn) ?? 0;
                }

                foreach (var (column, count) in counts)
                {
                    entity[column] = (entity.GetInt64(column) ?? 0) + count;
                }

                // Limited requests are stored but don't count toward the total the cap reads
                var total = (entity.GetInt64(TotalColumn) ?? 0) +
                    counts.Where(c => c.Key != UsageMeter.Limited).Sum(c => c.Value);
                entity[TotalColumn] = total;
                if (existing.HasValue)
                {
                    await table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
                }
                else
                {
                    await table.AddEntityAsync(entity, cancellationToken);
                }

                return total;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                // Another instance wrote the row first; read it again
            }
        }

        throw new InvalidOperationException($"Usage row {clientId}/{day} kept changing; will retry");
    }
}

/// <summary>
/// Per-client usage: counted in memory per instance and flushed to the usage table every 30
/// seconds. The daily cap reads the table's total plus this instance's unflushed counts, so
/// across instances it's accurate to within one flush interval.
/// </summary>
/// <remarks>
/// Storage failures fail open: the counts stay in memory and go out with a later flush, and the
/// cap keeps working from what this instance has seen. A Storage outage never rejects a request
/// by itself.
/// </remarks>
public class UsageMeter(IUsageTable table, ILogger<UsageMeter> logger, TimeProvider time)
{
    // Requests turned away by the rate limit or the daily cap; counted, but not toward the cap
    public const string Limited = "limited";

    private readonly Lock _gate = new();
    private Dictionary<(string Client, string Day), Dictionary<string, long>> _pending = [];
    private readonly Dictionary<(string Client, string Day), long> _stored = [];

    public static string Day(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd");

    public void Record(string clientId, string result)
    {
        var key = (clientId, Day(time.GetUtcNow()));
        lock (_gate)
        {
            if (!_pending.TryGetValue(key, out var counts))
            {
                _pending[key] = counts = [];
            }

            counts[result] = counts.GetValueOrDefault(result) + 1;
        }
    }

    /// <summary>Today's calls for the client, not counting limited ones.</summary>
    public long DailyTotal(string clientId)
    {
        var key = (clientId, Day(time.GetUtcNow()));
        lock (_gate)
        {
            var pending = _pending.TryGetValue(key, out var counts)
                ? counts.Where(c => c.Key != Limited).Sum(c => c.Value)
                : 0;
            return _stored.GetValueOrDefault(key) + pending;
        }
    }

    public async Task Flush(CancellationToken cancellationToken)
    {
        var today = Day(time.GetUtcNow());
        Dictionary<(string Client, string Day), Dictionary<string, long>> batch;
        List<(string Client, string Day)> keys;
        lock (_gate)
        {
            batch = _pending;
            _pending = [];
            foreach (var old in _stored.Keys.Where(k => k.Day != today).ToList())
            {
                _ = _stored.Remove(old);
            }

            // Also read back the clients seen today with nothing new here, to pick up the counts
            // other instances flushed
            keys = [.. batch.Keys.Union(_stored.Keys)];
        }

        foreach (var key in keys)
        {
            var counts = batch.GetValueOrDefault(key) ?? [];
            try
            {
                var total = await table.Add(key.Client, key.Day, counts, cancellationToken);
                lock (_gate)
                {
                    if (key.Day == today)
                    {
                        _stored[key] = total;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Requeue(key, counts);
            }
            catch (Exception ex)
            {
                // Fail open: whatever went wrong, keep the counts for the next flush
                logger.LogWarning(ex, "Could not flush usage for {Client} on {Day}; keeping it for the next flush",
                    key.Client, key.Day);
                Requeue(key, counts);
            }
        }
    }


    private void Requeue((string Client, string Day) key, Dictionary<string, long> counts)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(key, out var pending))
            {
                _pending[key] = pending = [];
            }

            foreach (var (result, count) in counts)
            {
                pending[result] = pending.GetValueOrDefault(result) + count;
            }
        }
    }
}

/// <summary>Flushes usage every 30 seconds, and once more on shutdown.</summary>
public class UsageFlusher(UsageMeter meter, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(FlushInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await meter.Flush(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await meter.Flush(cancellationToken);
    }
}
