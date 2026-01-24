namespace m4d.Services.Diagnostics;

/// <summary>
/// Represents a point-in-time snapshot of garbage collection and memory metrics.
/// </summary>
public record GcSnapshot(
    long TotalMemoryBytes,
    long HeapSizeBytes,
    long FragmentedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long WorkingSetBytes,
    double MemoryLoadPercent,
    long TotalAvailableMemoryBytes,
    long HighMemoryLoadThresholdBytes,
    long LargeObjectHeapSizeBytes,
    long PinnedObjectHeapSizeBytes,
    DateTimeOffset CapturedAt
)
{
    /// <summary>
    /// Gets the total memory in megabytes.
    /// </summary>
    public double TotalMemoryMB => TotalMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Gets the heap size in megabytes.
    /// </summary>
    public double HeapSizeMB => HeapSizeBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Gets the fragmented memory in megabytes.
    /// </summary>
    public double FragmentedMB => FragmentedBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Gets the working set in megabytes.
    /// </summary>
    public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Gets the large object heap size in megabytes.
    /// </summary>
    public double LargeObjectHeapSizeMB => LargeObjectHeapSizeBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Gets the pinned object heap size in megabytes.
    /// </summary>
    public double PinnedObjectHeapSizeMB => PinnedObjectHeapSizeBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Gets the fragmentation percentage of the heap.
    /// </summary>
    public double FragmentationPercent => HeapSizeBytes > 0 ? (FragmentedBytes * 100.0 / HeapSizeBytes) : 0;
}
