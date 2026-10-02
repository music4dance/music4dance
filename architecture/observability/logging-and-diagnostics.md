# Logging and Runtime Diagnostics

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/Configuration/M4dApplicationExtensions.cs` (logging setup),
`m4d/Services/Diagnostics/` (`GcDiagnostics`, `GcSnapshot`, `DumpResult`),
`m4d/Controllers/AdminController.cs`, `m4d/Views/Admin/Diagnostics.cshtml`

Where music4dance's application logs go, and what runtime memory diagnostics the app exposes.
Security and traffic diagnostics on the same admin page are covered in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md). Service-health reporting is
covered in [service-resilience](../infrastructure/service-resilience.md).

## Application logs

**Providers.** Logging is reset and configured in `M4dApplicationExtensions`, with two providers:
`AddConsole()` and `AddAzureWebAppDiagnostics()`. Log levels come from configuration
(`appsettings*.json` / App Configuration). Startup also writes progress lines with
`Console.WriteLine`, including the service-health startup report.

**Persistence (both `msc4dnc` and `m4d-test`, since 2026-09-04).** App Service logging is set to
**Filesystem, level Warning**, with container stdout/stderr also captured:

```
az webapp log config --name <app> --resource-group m4d-Web \
  --application-logging filesystem --level warning \
  --docker-container-logging filesystem
```

- Logs land in `/home/LogFiles/Application/` on the instance. Read them through Kudu,
  `az webapp log download`, or `az webapp log tail`. See
  [runbooks/read-production-logs](../runbooks/read-production-logs.md).
- It's a **rolling, quota-bound window**: free, but not durable or long-term, not queryable, and
  without alerting.
- On Linux, the Portal's toggle only offers "Gather STDOUT/STDERR", with no level control. Set both
  CLI flags together. The command overwrites the whole logs configuration; when it was applied,
  HTTP-log retention changed to 3 days / 100 MB, which was accepted.

**Not in use:**
- Application Insights isn't connected (there's no `APPLICATIONINSIGHTS_CONNECTION_STRING`).
- No diagnostic settings route to Log Analytics.
- An **orphaned** App Insights component `m4d-staging`, and its `DefaultWorkspace-…-WUS` Log
  Analytics workspace (pay-as-you-go, **no daily cap**), still exist in `DefaultResourceGroup-WUS`.
  When App Insights *was* wired in, its uncapped cost scaled with traffic and caused a billing
  surprise.

Durable or queryable options, and what to do about those resources, are in
[plans/log-persistence-options](../plans/log-persistence-options.md).

## Memory and GC diagnostics

**Memory & GC Diagnostics** section on `/Admin/Diagnostics`. Every admin diagnostics page captures
a fresh snapshot through the `SetupDiagnostics` filter.

| Action | Role | What it does |
| --- | --- | --- |
| *(page load)* / `GET /Admin/CaptureGcSnapshot` | `showDiagnostics` | `GcDiagnostics.CaptureSnapshot()`: total managed memory, heap size, fragmented bytes, Gen0/1/2 collection counts, working set, memory-load percentage, available memory and high-load threshold, **Large Object Heap and Pinned Object Heap sizes**. `CaptureGcSnapshot` also logs it at Information |
| `POST /Admin/ForceGarbageCollection` | `dbAdmin` | Full blocking, compacting Gen2 collection, run twice around `WaitForPendingFinalizers`. Shows before, after, and the bytes freed. Use sparingly in production |
| `POST /Admin/CreateMemoryDump` (`Mini` / `Heap` / `Full`) | `dbAdmin` | Writes a dump of the running process. Tries `dotnet-dump collect`, then falls back to the runtime's own `createdump`. Logged at Warning |
| `GET /Admin/DownloadDump`, `POST /Admin/DeleteDump`, `POST /Admin/DeleteAllDumps` | `dbAdmin` | Manage dump files. Download is restricted to the dump directory |

**Dump location:** `GcDiagnostics.DefaultDumpDirectory` is
`LocalApplicationData/m4d/dumps`, which is `~/.local/share/m4d/dumps` on Linux. The page lists
recent dumps. Heap and full dumps are large; delete them after downloading. The procedure is in
[runbooks/capture-memory-diagnostics](../runbooks/capture-memory-diagnostics.md).

## Future improvements

- **Durable log storage, query and alerting:**
  [plans/log-persistence-options](../plans/log-persistence-options.md) (Blob, Log Analytics with a
  hard cap, or a tuned App Insights).
- **Memory history and trends, allocation tracking, and a memory-pressure health check:**
  [plans/memory-diagnostics-next](../plans/memory-diagnostics-next.md).

## History

- 2025–2026: GC diagnostics (Phase 1) added to `/Admin/Diagnostics`. Dump capture and
  management, and LOH/POH sizes, followed. These were pieces of the original Phase 3.
- 2026-09-04: "Missing production warnings" investigated. Nothing persisted logs, because Log
  Stream is a live tap only. Filesystem application logging was enabled on both apps.
- 2026-10-01: Consolidated from `application-log-persistence-plan.md` and
  `memory-diagnostics-plan.md`. The unimplemented options and phases moved to `plans/`.

## Related

- [runbooks/read-production-logs](../runbooks/read-production-logs.md)
- [runbooks/capture-memory-diagnostics](../runbooks/capture-memory-diagnostics.md)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md)
- [individual-artists](../songs/individual-artists.md): artist-index memory measurements, which used these tools
