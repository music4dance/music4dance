# Logging and Runtime Diagnostics

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-16
**Code:** `m4d/Configuration/M4dApplicationExtensions.cs` (logging setup),
`m4d/Services/Diagnostics/` (`GcDiagnostics`, `GcSnapshot`, `DumpResult`),
`m4d/Controllers/AdminController.cs`, `m4d/Views/Admin/Diagnostics.cshtml`,
`m4d/Configuration/RequestLogSetup.cs` and `m4d/Middleware/RequestLogFieldsMiddleware.cs`
(app request log)

Where music4dance's application logs go, and what runtime memory diagnostics the app exposes.
Security and traffic diagnostics on the same admin page are covered in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md). Service-health reporting is
covered in [service-resilience](../infrastructure/service-resilience.md).

## Application logs

**Providers.** Logging is reset and configured in `M4dApplicationExtensions`, with two providers:
`AddConsole()` and `AddAzureWebAppDiagnostics()`. Log levels come from configuration
(`appsettings*.json` / App Configuration). Startup also writes progress lines with
`Console.WriteLine`, including the service-health startup report.

**Runtime log level.** The "Set to …" buttons and "Emit … log" links on `/Admin/Diagnostics` call
`AdminController.SetLogLevel` (POST with antiforgery), which changes the minimum level in memory
until the next restart, and `TestLog`, which writes one message at a chosen level. Both, and
`ThrowException`, require `showDiagnostics`.

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
- No diagnostic settings route to Log Analytics. The only diagnostic setting sends HTTP request
  logs to storage (next section).
- An **orphaned** App Insights component `m4d-staging`, and its `DefaultWorkspace-…-WUS` Log
  Analytics workspace (pay-as-you-go, **no daily cap**), still exist in `DefaultResourceGroup-WUS`.
  When App Insights *was* wired in, its uncapped cost scaled with traffic and caused a billing
  surprise.

Durable or queryable options, and what to do about those resources, are in
[plans/log-persistence-options](../plans/log-persistence-options.md).

## HTTP request logs

One record per request, written by App Service's front ends, for `msc4dnc` only (since
2026-10-29). Use them to see who is sending traffic: by client IP, user agent, URL, status and
response time.

**Why a diagnostic setting.** The App Service "web server logging" switch
(`httpLogs.fileSystem`) only drives IIS, which writes W3C logs to `/LogFiles/http/RawLogs` on
Windows plans. A Linux plan has no IIS: requests go from the platform front ends straight to
Kestrel in the container. The API still accepts the switch, and `az webapp log show` reports it
enabled, but nothing writes an `http` folder. On Linux the front-end request records are only
available through the `AppServiceHTTPLogs` category of a diagnostic setting.

**Setup:**

| Piece | Value |
| --- | --- |
| Diagnostic setting | `http-logs` on `msc4dnc`, category `AppServiceHTTPLogs` only |
| Storage account | `m4dlogs` in `m4d-Web`, West US (must match the app's region), Standard LRS, StorageV2, anonymous blob access off |
| Container | `insights-logs-appservicehttplogs`, created by Azure on first write |
| Retention | Lifecycle rule `delete-http-logs-after-30-days`: deletes append and block blobs under that container prefix 30 days after last modification. The diagnostic setting's own retention option is retired for storage |
| Read access | `Storage Blob Data Reader` on the account, used with `--auth-mode login`. Owner alone doesn't grant blob reads that way |

```bash
az monitor diagnostic-settings create --name http-logs --resource <msc4dnc resource id> \
  --storage-account <m4dlogs resource id> \
  --logs '[{"category":"AppServiceHTTPLogs","enabled":true}]'
```

**Format.** One append blob per hour at
`resourceId=/SUBSCRIPTIONS/<sub>/RESOURCEGROUPS/M4D-WEB/PROVIDERS/MICROSOFT.WEB/SITES/MSC4DNC/y=YYYY/m=MM/d=DD/h=HH/m=00/PT1H.json`,
holding one JSON record per line. The path's date and hour are UTC. `properties` is itself a
JSON-encoded *string*, so parse each line, then parse `properties` again. The useful fields in it
are `CIp` (the real client IP; `127.0.0.1` with `UserAgent` `AlwaysOn` is the platform's
keep-alive ping), `UserAgent`,
`CsMethod`, `CsUriStem`, `CsUriQuery`, `ScStatus`, `TimeTaken` (ms), `ScBytes`, `CsBytes`,
`Referer` and `CsHost`. Records arrive a few minutes after the request.

**Volume and cost.** About 250k requests a day, a few hundred MB of logs a day; storage costs
well under $1 a month at 30 days' retention. Log Analytics was considered and rejected for cost.

**Privacy.** Records hold full client IPs, query strings, referrers and a `Cookie` field. Keep
the account private, read with Entra ID (no account keys or SAS URLs), and keep logs local under
`local/`, which is gitignored.

The legacy `httpLogs.fileSystem` switch is still on (3 days / 100 MB) but writes nothing. Turn it
off with `az webapp log config -n msc4dnc -g m4d-Web --web-server-logging off` when convenient.
Reading the logs is in [runbooks/read-production-logs](../runbooks/read-production-logs.md#http-request-logs).

**Behind Cloudflare,** `CIp` in these records is Cloudflare's edge server, not the visitor: the
platform log has no header fields, so it can't record `CF-Connecting-IP`. For per-visitor
analysis use the app request log below. This log is still the cross-check, it's the only one with
response bytes, and it records requests that never reach the app (front-end 502s, platform
timeouts).

## App request log

One line per request with the visitor's IP, written by ASP.NET Core's built-in W3C logger
(`AddW3CLogging` / `UseW3CLogging`), set up in `RequestLogSetup`. It sits right after
`CloudflareClientIpMiddleware` at the top of the pipeline, so it covers static files and records
the status the exception handler settles on (an unhandled exception is a `500`).

**The three custom fields.** The logger only records request headers, so
`RequestLogFieldsMiddleware` (just before it) writes three that it then logs, always overwriting
whatever a client sent:

| Header (column `cs(...)`) | Value |
| --- | --- |
| `X-M4d-Log-Client` | The visitor, from the `ClientAddresses` feature: `CF-Connecting-IP` when relayed from Cloudflare's ranges, else the connecting address (normalized) |
| `X-M4d-Log-Relay` | The address that handed the request to App Service: the Cloudflare edge when proxied |
| `X-M4d-Log-Cookies` | Cookie **names** only (`_ga=;music4dance=;`). The logger's own `Cookie` field would record values, session tickets included, so it's off |

**Format.** Daily files `requests-YYYYMMDD.NNNN.txt` (UTC; a new `NNNN` at each restart or at
`FileSizeLimitMB`). A `#Fields:` header names the space-separated columns: `date time cs-method
cs-uri-stem cs-uri-query sc-status time-taken cs-version cs-host cs(User-Agent) cs(Referer)` and
the three above. Empty values are `-`, whitespace inside a value is written as `+` (so a literal
`+` in a user agent can't be told from a space), `cs-uri-query` keeps its `?`, and `time-taken`
is milliseconds in the app. **Response bytes aren't recorded**: the W3C logger has no such field.
Use the platform log for bytes.

**How the logger behaves** (read from `FileLoggerProcessor` in the 10.0 source and confirmed
with a probe, 2026-11-16):

- Lines go into an in-memory queue of 1,024. A background loop drains it, appending each line and
  flushing after every one, then sleeps `FlushInterval` (1 second) when the queue is empty.
- **A failed write** (I/O error, directory gone) drops that batch. The only report is a
  `Debug`-level message from `Microsoft.AspNetCore.HttpLogging.FileLoggerProcessor`, which
  production doesn't show, so lost lines are silent. Writing resumes on its own.
- **A slow or hung write blocks requests.** When the queue is full, `Add` blocks the request
  thread until the loop catches up. With the loop asleep for 15 seconds, 1,300 requests took
  14.7 seconds. So `FlushInterval` stays at its 1-second default (it can't be raised safely),
  and the log directory has to be storage that doesn't stall for long.
- On shutdown it stops without draining, so up to about a second of lines can be lost.
- It deletes the oldest files past `RetainedFileCountLimit`.

**Where the files go** is `RequestLog:Directory`. Two choices, both supported by
`fetch_http_logs.py --source app --from share|home`:

| | Azure Files share (`/mounts/request-logs`) | `/home/LogFiles/requests` |
| --- | --- | --- |
| Space | Its own quota, off the plan | Shares the plan's 11 GB (2 GB used, 2026-11-16); about 100 MB a day at current traffic |
| Cost | Transactions: about one write and two metadata operations per request, plus a directory listing about once a second. At about 360k requests a day, roughly $20–30 a month on the transaction-optimized tier (list prices 2026-11-16: $0.015 per 10k writes and lists); about four times that on Hot | None beyond the plan |
| Stall risk | A new dependency: an SMB stall longer than about 1,024 requests' worth blocks requests | None new: the site already runs from `/home`, so a `/home` stall takes it down anyway |
| Auth | The mount uses the account key; rotating it breaks the mount until it's updated | n/a |
| Retention | `RetainedFileCountLimit` (30 is fine) | `RetainedFileCountLimit` (keep it to about 14) |

**Settings:**

| Key | Default | Notes |
| --- | --- | --- |
| `RequestLog:Enabled` | `false` | Read at startup, so restart after changing it |
| `RequestLog:Directory` | none | An absolute path that already exists. The app doesn't create it, so a missing mount can't quietly become a folder on the container's own disk |
| `RequestLog:RetainedFileCountLimit` | `14` | 1–1000 |
| `RequestLog:FileSizeLimitMB` | `200` | 1–2000. Large enough that a day is normally one file, so the count above is about that many days |

A bad value turns the log off with a `[RequestLog] WARNING: disabled, ...` line at startup
instead of failing startup. When it's on, startup shows `[RequestLog] Enabled: <directory>, ...`.

**One-time setup, Azure Files share** (PowerShell). Adding the mount restarts the app:

```powershell
az storage share-rm create --storage-account m4dlogs -g m4d-Web -n request-logs `
  --quota 20 --access-tier TransactionOptimized
$key = az storage account keys list -n m4dlogs -g m4d-Web --query "[0].value" -o tsv
az webapp config storage-account add -g m4d-Web -n msc4dnc --custom-id requestlogs `
  --storage-type AzureFiles --account-name m4dlogs --share-name request-logs `
  --access-key $key --mount-path /mounts/request-logs
az appconfig kv set -n music4dance --key RequestLog:Directory --value /mounts/request-logs --yes
az appconfig kv set -n music4dance --key RequestLog:RetainedFileCountLimit --value 30 --yes
az appconfig kv set -n music4dance --key RequestLog:Enabled --value true --yes
az webapp restart -n msc4dnc -g m4d-Web
```

**One-time setup, `/home`:** create the folder once (`az webapp ssh -n msc4dnc -g m4d-Web`, then
`mkdir -p /home/LogFiles/requests`), set `RequestLog:Directory` to `/home/LogFiles/requests` and
`RequestLog:Enabled` to `true`, and restart.

**Privacy.** The same rules as the HTTP request log: full client IPs and query strings, read only
by the site's owner, and kept under `local/` once downloaded. Cookie values are never recorded.

**Reading:** `python -I .claude/skills/analyze-traffic/fetch_http_logs.py --source app [--from home]`
downloads to `local/app-request-logs/`, and `analyze_http_logs.py` reads either source (one per
directory). See the `analyze-traffic` skill.

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
- 2026-10-02, #326 (`af5513b8`): `SetLogLevel`, `TestLog` and `ThrowException` require
  `showDiagnostics`; they had been `[AllowAnonymous]`.
- 2026-10-03: `SetLogLevel` is POST-only with antiforgery, so another site can't trigger it.
- 2026-10-29: HTTP request logs added: diagnostic setting `http-logs` to storage account
  `m4dlogs`, with 30-day retention. The filesystem HTTP-log switch, turned on during the OOM
  investigation, turned out to do nothing on Linux.
- 2026-11-16: App request log added with the built-in W3C logger, off until
  `RequestLog:Enabled`. Behind Cloudflare the HTTP request log's `CIp` is the edge server; this
  log records the visitor. It replaced a custom middleware-and-blob design (PR #406), trading
  response bytes for much less code.

## Related

- [runbooks/read-production-logs](../runbooks/read-production-logs.md)
- [runbooks/capture-memory-diagnostics](../runbooks/capture-memory-diagnostics.md)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md)
- [individual-artists](../songs/individual-artists.md): artist-index memory measurements, which used these tools
