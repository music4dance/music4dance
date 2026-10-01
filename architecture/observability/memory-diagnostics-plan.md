# Memory Diagnostics Plan for music4dance.net

## Overview

This document outlines a plan to add memory diagnostic capabilities to the application to help investigate and optimize memory pressure issues.

## Goals

1. **Visibility**: Provide real-time insight into memory usage patterns
2. **Investigation**: Enable capture of memory snapshots for detailed analysis
3. **Monitoring**: Track memory trends over time to identify leaks or pressure points
4. **Action**: Provide tools to trigger garbage collection and measure impact

---

## Phase 1: [COMPLETE] Core GC Diagnostics (Low Effort, High Value)

### 1.1 Add Inline GC Stats to Diagnostics Page

Add a new section to `Views/Admin/Diagnostics.cshtml` showing current GC state:

**Metrics to display:**

- `GC.GetTotalMemory(false)` - Current estimated managed heap size
- `GC.GetGCMemoryInfo()` - Detailed GC memory info including:
  - `HeapSizeBytes` - Total heap size
  - `FragmentedBytes` - Fragmented memory
  - `MemoryLoadBytes` - Total memory load
  - `TotalAvailableMemoryBytes` - Available system memory
  - `HighMemoryLoadThresholdBytes` - High memory threshold
  - `Generation` counts and sizes
- `GC.CollectionCount(0/1/2)` - Collection counts per generation
- `Environment.WorkingSet` - Process working set

**Implementation:**

```csharp
// In AdminController or a new MemoryDiagnosticsController
public record GcSnapshot(
    long TotalMemoryBytes,
    long HeapSizeBytes,
    long FragmentedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long WorkingSetBytes,
    double MemoryLoadPercent,
    DateTimeOffset CapturedAt
);
```

### 1.2 Add "Capture GC Snapshot" Link

Add a link on the Diagnostics page that captures and logs a snapshot:

```csharp
[Authorize(Roles = "showDiagnostics")]
[HttpGet]
public ActionResult CaptureGcSnapshot()
{
    var snapshot = GcDiagnostics.CaptureSnapshot();
    Logger.LogInformation("GC Snapshot captured: {Snapshot}", snapshot);
    ViewBag.CapturedSnapshot = snapshot;
    return View("Diagnostics");
}
```

### 1.3 Force GC and Measure Impact

Endpoint to force full GC and report before/after memory freed:

```csharp
[HttpPost]
[Authorize(Roles = "dbAdmin")]
public ActionResult ForceGarbageCollection()
{
    var before = GcDiagnostics.CaptureSnapshot();

    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

    var after = GcDiagnostics.CaptureSnapshot();

    ViewBag.GcBefore = before;
    ViewBag.GcAfter = after;
    ViewBag.MemoryFreed = before.TotalMemoryBytes - after.TotalMemoryBytes;

    return View("Diagnostics");
}
```

**Warning**: This should be admin-only and used sparingly in production.

---

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

### 3.2 Memory Dump Trigger

For serious investigation, add ability to trigger a mini-dump:

```csharp
[HttpPost]
[Authorize(Roles = "dbAdmin")]
public ActionResult TriggerMiniDump()
{
    // Use System.Diagnostics or createdump tool
    // This creates a file for offline analysis with dotnet-dump/Visual Studio
}
```

**Note**: Large dumps, use cautiously. Consider gcdump for managed-only analysis.

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

## Proposed File Structure

```
m4d/
??? Controllers/
?   ??? AdminController.cs          # Add GC endpoints here (or new controller)
??? Services/
?   ??? Diagnostics/
?   ?   ??? GcDiagnostics.cs        # Static helper for capturing snapshots
?   ?   ??? GcSnapshot.cs           # Record type for snapshot data
?   ?   ??? GcSnapshotHistory.cs    # Ring buffer for history
?   ?   ??? GcMonitoringService.cs  # Background service (optional)
??? Views/
?   ??? Admin/
?       ??? Diagnostics.cshtml      # Add GC section
?       ??? GcResults.cshtml        # Before/after GC view (optional)
??? ViewModels/
?   ??? GcDiagnosticsViewModel.cs   # View model for diagnostics display
```

---

## Implementation Priority

| Phase | Feature                  | Effort | Value  | Priority |
| ----- | ------------------------ | ------ | ------ | -------- |
| 1.1   | Inline GC stats display  | Low    | High   | **P0**   |
| 1.2   | Capture snapshot link    | Low    | High   | **P0**   |
| 1.3   | Force GC endpoint        | Low    | High   | **P0**   |
| 2.1   | Snapshot history buffer  | Medium | Medium | P1       |
| 2.2   | Background sampling      | Medium | Medium | P1       |
| 2.3   | Trend visualization      | Medium | Medium | P1       |
| 4.2   | Memory health check      | Low    | High   | P1       |
| 3.1   | EventCounter integration | Medium | Medium | P2       |
| 4.1   | App Insights metrics     | Low    | Medium | P2       |
| 3.2   | Mini-dump trigger        | Medium | Low    | P3       |

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

## Next Steps

1. ? Create this plan document
2. ? Implement Phase 1.1 - Add GC stats section to Diagnostics.cshtml
3. ? Implement Phase 1.2 - Add CaptureGcSnapshot link
4. ? Implement Phase 1.3 - Add ForceGarbageCollection endpoint
5. ? Review and iterate based on findings
