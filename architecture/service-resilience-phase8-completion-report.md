# Service Resilience Phase 8: Search Service Throttling & Auto-Recovery

**Date**: September 3, 2026
**Status**: ✅ COMPLETE

## Overview

Phase 8 closes two gaps exposed by a real Azure AI Search "capacityOverloaded" throttling
incident: a 503/429 from a live search query wasn't recognized as a service-availability
condition at all (so it could surface as a raw unhandled exception instead of graceful
degradation), and `ServiceHealthManager` had no way to recover from an `Unavailable` mark once
made — for most services, including `SearchService`, nothing in production ever called
`MarkHealthy` again. Combined, fixing only the first problem would have made things worse: the
very first transient throttling spike would have wedged the entire site's search functionality
until the next app restart, instead of self-healing in seconds like the underlying Azure issue
does.

## Problem Statement

On 2026-09-03 ~21:12–21:15 UTC, Azure AI Search returned `503 Service Unavailable` with
`throttle-reason: capacityOverloaded` for at least 17 distinct queries over ~13 seconds, with
response latency climbing from ~5.6s to ~13s as the SDK's built-in retry policy (3 attempts,
exponential backoff) repeatedly failed before giving up. The Azure Portal's "Throttled Search
Queries" metric later confirmed a ~14% throttled-query rate at the peak, with a handful of
smaller similar spikes over the trailing 30 days and ~0% otherwise.

Tracing what happened to one of these failures end-to-end surfaced two separate bugs:

1. **Misclassified exception** (`m4dModels/SongIndex.cs`, `DoSearch`): the only recognized
   failure mode was `InvalidOperationException` from a missing/misconfigured `TokenCredential`
   (Phase 6). A genuine `RequestFailedException` with `Status == 503` fell into the generic
   `catch (Exception ex)` block instead, which assumes an unrelated field-selection mismatch and
   retries immediately with **no backoff** against the still-overloaded service. On a second
   failure it throws an `AggregateException` — which none of the ~8 `catch (InvalidOperationException
   ex) when (IsSearchServiceError(ex))` blocks across `SongController` recognize — so it became
   an unhandled exception (very likely surfacing as a raw 500) instead of the graceful
   degradation the Phase 6 work was built to provide.

2. **No recovery path** (`m4d/Services/ServiceHealth/ServiceHealthManager.cs`): once
   `MarkUnavailable("SearchService", ...)` is called, ~19 call sites across `SongController`
   (plus several API controllers) gate on `IsServiceHealthy("SearchService")`/`IsSearchAvailable()`
   *before* even attempting a search — so nothing would ever try Azure Search again to notice it
   had recovered. This was already known and documented (`architecture/admin-pages.md` explicitly
   noted "nothing in production ever calls `MarkHealthy(\"SearchService\")`"), but had never been
   exercised for `SearchService` specifically until this incident, because no code path had ever
   correctly classified a search failure as "unavailable" in the first place. The App Service
   `/api/health` endpoint (what the single-instance deployment's 1-hour health-check auto-replace
   watches) only checks the Database, not Search, so that safety net doesn't cover this either.

Fixing #1 alone would have meant: the *first* throttling spike (which self-heals in seconds)
instead wedges search app-wide until the next deploy or platform restart — worse than the
pre-fix behavior of a handful of ugly 500s that resolved themselves once the spike passed.

## Implementation

### 1. Recognize 503/429 as a service-availability condition

**File**: `m4dModels/SongIndex.cs`, `DoSearch`

```csharp
catch (RequestFailedException ex) when (ex.Status == 503 || ex.Status == 429)
{
    // The service is overloaded/throttled (e.g. throttle-reason: capacityOverloaded) rather
    // than something local to this query, so surface it as the same "unavailable" condition
    // the upper layers already know how to handle instead of falling into the
    // retry-without-select path below, which assumes a field-selection mismatch and would just
    // hammer an already-overloaded service with an immediate, un-backed-off second attempt.
    throw new InvalidOperationException("Azure Search service is unavailable", ex);
}
```

Placed before the generic `catch (Exception ex)` fallback, reusing the exact
`InvalidOperationException("Azure Search service is unavailable")` shape the Phase 6
TokenCredential case already produces, so it flows through the existing
`IsSearchServiceError` → `HandleSearchServiceError` → `ServiceHealth.MarkUnavailable` chain with
no changes needed at any call site.

### 2. Cooldown-based self-recovery

**File**: `m4d/Services/ServiceHealth/ServiceHealthManager.cs`

Added `UnavailableCooldown` (default 1 minute, `internal` setter for tests). `IsServiceHealthy`
now optimistically returns `true` once the cooldown since the last failure (`LastChecked`) has
elapsed, instead of staying `false` forever:

```csharp
public bool IsServiceHealthy(string serviceName)
{
    if (!_serviceStatuses.TryGetValue(serviceName, out var status))
    {
        return true; // Unknown - optimistic
    }

    return status.Status != ServiceStatus.Unavailable
        || DateTime.UtcNow - status.LastChecked >= UnavailableCooldown;
}
```

If the service is still down, the next real attempt fails again, `MarkUnavailable` resets
`LastChecked`, and the cooldown restarts — so a sustained outage gets retried at roughly the
cooldown interval rather than on every single request. This alone is enough to prevent a
permanent wedge, but the underlying `Status` field stays stale at `Unavailable` until something
explicitly reports success (see next section) — meaning `/api/health` and the health report would
keep showing "Unavailable" for a while after functional recovery.

### 3. Explicit success signal (closes the dashboard-staleness gap)

**Files**: `m4dModels/SearchServiceInfo.cs`, `m4dModels/SongIndex.cs`,
`m4d/Configuration/M4dApplicationExtensions.cs`

`m4dModels` sits below `m4d` in the project graph and cannot reference
`m4d.Services.ServiceHealth.ServiceHealthManager` directly. Bridged the signal across that
boundary instead of restructuring the dependency graph:

- `ISearchServiceManager` gained a default (no-op) interface method:
  ```csharp
  void ReportSearchSuccess() { }
  ```
  The no-op default means `m4dModels.Sandbox`'s `LocalSearchServiceManager` needs no changes at
  all.
- The production `SearchServiceManager` (also in `m4dModels`) exposes a settable delegate:
  ```csharp
  public Action OnSearchSuccess { get; set; }
  public void ReportSearchSuccess() => OnSearchSuccess?.Invoke();
  ```
- `SongIndex.DoSearch` — the single low-level chokepoint nearly every Azure Search call funnels
  through (search, streaming, facets, backup/reindex) — calls `Manager.ReportSearchSuccess()`
  right after each of its two success paths (the primary call and the retry-without-select
  fallback).
- `m4d`'s DI composition root wires the bridge where `ISearchServiceManager` is registered:
  ```csharp
  services.AddSingleton<ISearchServiceManager>(sp =>
  {
      var manager = ActivatorUtilities.CreateInstance<SearchServiceManager>(sp);
      manager.OnSearchSuccess = () => serviceHealth.MarkHealthy("SearchService");
      return manager;
  });
  ```
  (`serviceHealth` here is the same already-constructed `ServiceHealthManager` singleton instance
  from earlier in `AddM4dServices`, captured by closure — no need to resolve it from the
  container.)

Net effect: recovery is now immediate and correct — the moment any search actually succeeds,
`MarkHealthy` fires (`Status` → `Healthy`, `ConsecutiveFailures` → 0, `ErrorMessage` cleared), so
`/api/health`, the health report, and the Diagnostics view all reflect reality right away instead
of only functionally-recovering-but-still-reporting-stale-`Unavailable` via cooldown inference
alone.

### Cross-layer notification pattern

This bridge (lower layer exposes a settable no-op delegate on an interface it already owns; the
host's composition root wires it to whatever cross-cutting concern it tracks) is a reusable
pattern for signaling upward across the `m4dModels` → `m4d` boundary without a circular project
reference. Worth reaching for again if a similar need comes up for another service tracked by
`ServiceHealthManager` (e.g. `Database`, `AppConfiguration`).

## Key Deliverables

- ✅ A 503/429 from a live search query degrades gracefully (empty results / "temporarily
  unavailable" message) instead of risking an unhandled `AggregateException`
- ✅ A transient throttling spike self-heals within one cooldown period (default 1 minute) of the
  underlying service recovering, with no app restart required
- ✅ Recovery is detected and reported immediately on the first successful retry, not just
  inferred after a timeout — admin-facing health status stays accurate
- ✅ Sustained outages are retried at roughly the cooldown interval rather than hammered on every
  request

## Known Follow-Up (not done in this phase)

- The `song-index` Vue page (`m4d/ClientApp/src/pages/song-index/App.vue`) has no `v-else` for
  its `searchAvailable` check — when search is marked unavailable it renders site chrome only,
  with no "temporarily unavailable" messaging in the body. This is the same gap already tracked
  as future work item 10 ("Song List Banner") in `service-resilience-plan.md`; this incident
  makes it more likely to actually be hit in practice, but the fix itself is unchanged in scope.
- The root cause of *why* Search got overloaded (traffic spike vs. a concurrent reindex/backup job
  vs. an undersized SU/replica count for current load) is still open — needs the Search resource's
  own metrics in the Azure Portal, not something visible from application logs.

## Files Changed

| File | Change |
| --- | --- |
| `m4dModels/SongIndex.cs` | New `catch (RequestFailedException ex) when (ex.Status == 503 \|\| 429)` in `DoSearch`; `Manager.ReportSearchSuccess()` on both success paths; made `Client` and `Manager` `protected virtual` for fault-injection testing |
| `m4dModels/SearchServiceInfo.cs` | `ISearchServiceManager.ReportSearchSuccess()` (default no-op); `SearchServiceManager.OnSearchSuccess` + implementation |
| `m4d/Services/ServiceHealth/ServiceHealthManager.cs` | `UnavailableCooldown` (default 1 minute, `internal` setter); `IsServiceHealthy` now cooldown-aware |
| `m4d/Configuration/M4dApplicationExtensions.cs` | `ISearchServiceManager` registration wires `OnSearchSuccess` to `serviceHealth.MarkHealthy("SearchService")` |
| `m4dModels.Tests/SongIndexSearchAvailabilityTests.cs` | **New** — 503/429 classification, non-throttling errors unaffected, success reporting |
| `m4dModels.Tests/SearchServiceManagerVersioningTests.cs` | Added `ReportSearchSuccess` bridge tests |
| `m4d.Tests/Services/ServiceHealthManagerTests.cs` | **New** — cooldown expiry, renewed-failure reset, immediate recovery via `MarkHealthy` |

## Testing

Unit tests only (no live Azure Search fault injection in CI):

- `SongIndexSearchAvailabilityTests`: a mocked `SearchClient` throwing `RequestFailedException`
  with `Status` 503/429 produces the recognized `InvalidOperationException`; a non-throttling
  status (400) still falls through to the pre-existing retry-without-select path unchanged; a
  successful call invokes `ISearchServiceManager.ReportSearchSuccess()` exactly once.
- `ServiceHealthManagerTests`: unknown service optimistic; `Unavailable` returns `false` before
  the cooldown elapses and `true` after; a renewed failure restarts the cooldown; `MarkHealthy`
  clears `Unavailable` immediately regardless of cooldown state.
- `SearchServiceManagerVersioningTests`: `ReportSearchSuccess()` invokes `OnSearchSuccess` when
  set, and is a safe no-op when unset (the Sandbox/no-health-tracking case).

Full suite at completion: `m4dModels.Tests` 491/492 (1 pre-existing unrelated skip),
`m4d.Tests` 215/215.
