# Capture Memory Diagnostics

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-29

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

### From the container (no admin page)

Use this when the page isn't reachable, or for a `gcdump`: a managed-heap graph of tens of MB
instead of a heap dump of about 1 GB. Collecting it forces a Gen2 GC and pauses the app for a few
seconds. The App Service .NET image ships the diagnostic tools in `/opt/dotnetcore-tools/`
(`dotnet-gcdump`, `dotnet-dump`, `dotnet-counters`, `dotnet-trace`).

1. Open a tunnel: `az webapp create-remote-connection -n msc4dnc -g m4d-Web --port 52222`
   (leave it running).
2. `ssh root@127.0.0.1 -p 52222`, password `Docker!` (the App Service SSH convention). For
   non-interactive use, an askpass script with `SSH_ASKPASS_REQUIRE=force` supplies it.
3. In the container:
   ```bash
   ps aux | grep "dotnet m4d.dll"        # PID
   mkdir -p /home/dumps
   /opt/dotnetcore-tools/dotnet-gcdump collect -p <pid> -o /home/dumps/m4d-<date>.gcdump
   /opt/dotnetcore-tools/dotnet-counters collect -p <pid> --counters System.Runtime \
     --format csv -o /home/dumps/counters-<date>.csv --duration 00:00:12
   ```
4. Download through Kudu VFS (`/home` is shared with Kudu):
   ```bash
   curl -H "Authorization: Bearer $(az account get-access-token --query accessToken -o tsv)" \
     -o local/dumps/m4d-<date>.gcdump \
     https://msc4dnc.scm.azurewebsites.net/api/vfs/dumps/m4d-<date>.gcdump
   ```
   Then delete `/home/dumps` and stop the tunnel.
5. Analyze with `dotnet-gcdump report <file>` (`dotnet tool install -g dotnet-gcdump`), or open
   it in Visual Studio or PerfView. The report's byte column doesn't add up to the heap total,
   so rank types by object count, or use PerfView for sizes.

Baseline, 2026-10-29 at 14 hours' uptime on B2: working set 463 MB; GC committed 170 MB; about
73 MB of live managed objects, with 52 MB of the 56 MB LOH being fragmentation. The top app types
were `SongProperty` (105k), `ArtistIndexEntry` (30k) and `AlbumDetails` (11k). The remaining
~290 MB is runtime, JIT code, mapped assemblies and native buffers.

## Verification

The snapshot and dump details appear on the page, and the dump file opens in `dotnet-dump analyze`.

## Rollback

Delete any dumps left on the instance. Forcing a GC and capturing a dump don't change app state,
though a full blocking GC or a large dump can pause the app briefly.
