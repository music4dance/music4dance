# Memory Diagnostics: Next Phases

**Type:** Plan
**Status:** Proposed. Phase 1 (GC stats, snapshot, forced collection) and the Phase 3 dump trigger
have shipped, along with LOH/POH sizes in the snapshot. History, trends, allocation tracking and
the integrations have not.
**Last verified:** 2026-10-01

Current capabilities are described in
[logging-and-diagnostics § Memory and GC diagnostics](../observability/logging-and-diagnostics.md#memory-and-gc-diagnostics).

## Phase 2: Memory Snapshot History & Trends (Medium Effort)

### 2.1 In-Memory Snapshot Ring Buffer

Store recent snapshots (last N captures or time window) for trend analysis:

```csharp
public class GcSnapshotHistory
{
    private readonly ConcurrentQueue<GcSnapshot> _snapshots = new();
    private readonly int _maxSnapshots = 100;

    public void Add(GcSnapshot snapshot) { ... }
    public IReadOnlyList<GcSnapshot> GetRecent(int count) { ... }
}
```

Register as singleton in DI.

### 2.2 Automatic Background Sampling

Optional periodic capture (configurable interval, e.g., every 60 seconds):

```csharp
public class GcMonitoringBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _snapshotHistory.Add(GcDiagnostics.CaptureSnapshot());
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
```

Control via feature flag: `FeatureFlags.GcMonitoring`

### 2.3 Trend Visualization

Add a simple chart or table showing memory over time on diagnostics page.

---

## Phase 3: Advanced Diagnostics (Higher Effort)

### 3.1 Object Allocation Tracking (dotnet-counters integration)

Add endpoint to capture EventCounter data:

- `gc-heap-size`
- `gen-0-gc-count`, `gen-1-gc-count`, `gen-2-gc-count`
- `alloc-rate`
- `gc-fragmentation`

Consider exposing via `/metrics` endpoint for external monitoring (Prometheus/Grafana).

### 3.2 Memory Dump Trigger (shipped)

Implemented: `CreateMemoryDump`, `DownloadDump`, `DeleteDump` and `DeleteAllDumps` on `/Admin/Diagnostics`. See [logging-and-diagnostics](../observability/logging-and-diagnostics.md).

### 3.3 Large Object Heap (LOH) Monitoring

Track LOH specifically since it's a common source of fragmentation:

```csharp
var gcInfo = GC.GetGCMemoryInfo();
// gcInfo.GenerationInfo[3] is LOH
// gcInfo.GenerationInfo[4] is POH (Pinned Object Heap)
```

---

## Phase 4: Integration with External Tools

### 4.1 Application Insights Integration

If using App Insights, emit custom metrics:

```csharp
_telemetryClient.GetMetric("GcHeapSize").TrackValue(heapSize);
_telemetryClient.GetMetric("GcGen2Collections").TrackValue(gen2Count);
```

### 4.2 Health Check for Memory Pressure

Add a memory health check:

```csharp
public class MemoryHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(...)
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var loadPercent = (double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes;

        if (loadPercent > 0.9)
            return Task.FromResult(HealthCheckResult.Unhealthy("Memory pressure critical"));
        if (loadPercent > 0.75)
            return Task.FromResult(HealthCheckResult.Degraded("Memory pressure elevated"));

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
```

### 4.3 dotnet-gcdump Support

Document how to capture GC dumps remotely:

```bash
dotnet-gcdump collect -p <pid>
```

Consider adding a diagnostic endpoint that returns the PID for easy scripting.

---

## Security Considerations

1. **Authorization**: All memory diagnostic endpoints must require `showDiagnostics` or `dbAdmin` role
2. **Rate Limiting**: Force GC and dump operations should be rate-limited
3. **No PII**: Ensure snapshots don't capture sensitive data
4. **Audit Logging**: Log when diagnostic actions are taken

---

## Useful References

- [GC.GetGCMemoryInfo](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getgcmemoryinfo)
- [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [dotnet-gcdump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump)
- [Memory management best practices](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/)

---

