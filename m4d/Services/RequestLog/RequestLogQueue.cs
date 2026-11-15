using System.Threading.Channels;

namespace m4d.Services.RequestLog;

/// <summary>
/// Requests waiting for the next flush. Bounded so a stalled flush can't grow memory: when it's
/// full, new requests are counted and dropped rather than queued.
/// </summary>
public sealed class RequestLogQueue
{
    private readonly int _capacity;
    private readonly Channel<RequestLogEntry> _channel;
    private long _dropped;

    public RequestLogQueue(int capacity)
    {
        _capacity = Math.Max(1, capacity);
        _channel = Channel.CreateBounded<RequestLogEntry>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public void Add(RequestLogEntry entry)
    {
        if (!_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    /// <summary>Everything queued now, up to one queue's worth.</summary>
    public List<RequestLogEntry> Drain()
    {
        var entries = new List<RequestLogEntry>();
        while (entries.Count < _capacity && _channel.Reader.TryRead(out var entry))
        {
            entries.Add(entry);
        }
        return entries;
    }

    /// <summary>Requests dropped because the queue was full, since the last call.</summary>
    public long TakeDropped() => Interlocked.Exchange(ref _dropped, 0);
}
