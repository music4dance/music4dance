using System.Collections.Concurrent;

namespace m4dApi;

/// <summary>
/// A token bucket per client, in memory on each instance: a client may make
/// <c>burst</c> requests at once, refilled at <c>ratePerSecond</c>. With more than one instance
/// the effective limit is that times the instance count, which the instance cap bounds.
/// </summary>
public class ClientRateLimiter(TimeProvider time)
{
    private sealed class Bucket
    {
        public double Tokens;
        public long Stamp;
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    /// <summary>
    /// Takes one token for <paramref name="clientId"/>. When none is left, returns false and how
    /// long until one is.
    /// </summary>
    public bool TryAcquire(string clientId, double ratePerSecond, int burst, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        var now = time.GetTimestamp();
        var bucket = _buckets.GetOrAdd(clientId, _ => new Bucket { Tokens = burst, Stamp = now });
        lock (bucket)
        {
            var elapsed = time.GetElapsedTime(bucket.Stamp, now).TotalSeconds;
            bucket.Tokens = Math.Min(burst, bucket.Tokens + (elapsed * ratePerSecond));
            bucket.Stamp = now;
            if (bucket.Tokens >= 1)
            {
                bucket.Tokens -= 1;
                return true;
            }

            retryAfter = TimeSpan.FromSeconds((1 - bucket.Tokens) / ratePerSecond);
            return false;
        }
    }
}
