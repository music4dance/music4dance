using System.Collections.Concurrent;

namespace m4dModels;

// General-purpose named event counters for lightweight production diagnostics -- e.g. how often a
// rare fallback/retry path (that we're trying to design away) actually gets hit. Call Increment
// from wherever the event happens; a new counter name shows up automatically wherever counters are
// surfaced (currently /Admin/Diagnostics) with no additional wiring needed there.
public static class DiagnosticCounters
{
    private static readonly ConcurrentDictionary<string, long> Counts = new();

    public static void Increment(string name)
    {
        Counts.AddOrUpdate(name, 1, (_, count) => count + 1);
    }

    public static IReadOnlyDictionary<string, long> Snapshot()
    {
        return new SortedDictionary<string, long>(Counts, StringComparer.Ordinal);
    }
}
