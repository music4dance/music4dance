# Capture Memory Diagnostics

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01

## When to use

Memory pressure, a suspected leak, slow restarts or OOMs, or measuring the memory cost of a
feature (the artist index work used this). What each action does is described in
[logging-and-diagnostics § Memory and GC diagnostics](../observability/logging-and-diagnostics.md#memory-and-gc-diagnostics).

## Prerequisites / access

- `showDiagnostics` to view snapshots.
- `dbAdmin` to force a GC or create, download or delete dumps.
- To analyze a dump: `dotnet-dump analyze` (`dotnet tool install -g dotnet-dump`), or Visual
  Studio / WinDbg.

## Steps

1. **Baseline.** Open `/Admin/Diagnostics` → **Memory & GC Diagnostics** and note the managed
   memory, heap, fragmentation, LOH/POH, working set, memory load and Gen2 count. **Capture GC
   Snapshot** also writes it to the log.
2. **Is it garbage or live?** Click **Force Garbage Collection**. It shows before and after, and
   the bytes freed.
   - Large savings mean the memory was collectable garbage, so the problem is GC timing or
     settings rather than a leak.
   - Little savings with high memory means something is holding it live. Go to step 3.
3. **Dump.** Choose **Heap** (usually enough for managed leaks) and click **Create Memory Dump**.
   - `Mini` is threads only and small.
   - `Full` is everything and very large. Avoid it in production unless you need native memory.

   It uses `dotnet-dump` if available, otherwise the runtime's `createdump`. The result shows the
   path and size, or an error.
4. **Download** the dump from the recent-dumps list, then **Delete** it, or use **Delete All**.
   Dumps live on the instance under `~/.local/share/m4d/dumps` and take up disk.
5. **Analyze locally:**
   ```bash
   dotnet-dump analyze <file>
   > dumpheap -stat          # biggest types by total size
   > dumpheap -mt <MT>       # instances of one type
   > gcroot <address>        # what keeps it alive
   ```

## Verification

The snapshot and dump details appear on the page, and the dump file opens in `dotnet-dump analyze`.

## Rollback

Delete any dumps left on the instance. Forcing a GC and capturing a dump don't change app state,
though a full blocking GC or a large dump can pause the app briefly.
