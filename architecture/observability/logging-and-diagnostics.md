# Logging and Runtime Diagnostics

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-15
**Code:** `m4d/Configuration/M4dApplicationExtensions.cs` (logging setup),
`m4d/Services/Diagnostics/` (`GcDiagnostics`, `GcSnapshot`, `DumpResult`),
`m4d/Controllers/AdminController.cs`, `m4d/Views/Admin/Diagnostics.cshtml`,
`m4d/Middleware/RequestLogMiddleware.cs`, `m4d/Services/RequestLog/` (app request log)

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
analysis use the app request log below. This log is still the cross-check, and it records requests
that never reach the app (front-end 502s, platform timeouts).

## App request log

The app's own per-request record, written by `RequestLogMiddleware` (second in the pipeline,
right after `CloudflareClientIpMiddleware`, so it covers static files too). It has the same fields as the HTTP request log, but `CIp` is the
visitor's address as `CloudflareClientIpMiddleware` resolved it (its `ClientAddresses` feature:
`CF-Connecting-IP`, trusted only when the request was relayed from Cloudflare's ranges). Bytes
are counted by a thin decorator over the response body feature: it adds no buffering and never
starts or completes the response, so Kestrel's choice of `Content-Length` or chunked encoding
is unchanged.

**How it works.** The middleware queues one record per request in memory (`RequestLogQueue`,
bounded at `MaxQueuedRecords`; past that, requests are dropped and counted). Every
`FlushIntervalSeconds`, `RequestLogFlushService` appends the queued records to their hourly
append blob in blocks of up to 1 MiB, written from one reused buffer, and it flushes once more
on shutdown. Each flush has its own 20-second timeout instead of the host's stopping token, so a
restart lets an in-flight append finish rather than cancelling it. A failed write drops that
batch rather than retrying, and a Warning reports the dropped count at most every 15 minutes. Several instances can
share an hour's blob, because each append block is atomic.

**Setup:**

| Piece | Value |
| --- | --- |
| Switch | `RequestLog:Enabled` (default `false`), read at startup, so restart after changing it. Other keys: `ServiceUri` (`https://m4dlogs.blob.core.windows.net`), `Container` (`app-request-logs`), `Prefix` (default: the site name from `WEBSITE_SITE_NAME`), `FlushIntervalSeconds` (60), `MaxQueuedRecords` (20,000, about 20 MB at most). An invalid `ServiceUri` or empty `Container` turns the log off with a `[RequestLog] WARNING` line at startup, instead of failing startup |
| Container | `app-request-logs` in `m4dlogs`. The app doesn't create it |
| Write access | The app's system-assigned identity has `Storage Blob Data Contributor`, scoped to the container |
| Retention | Lifecycle rule `delete-app-request-logs-after-30-days`, prefix `app-request-logs/`, append blobs, 30 days after last modification |

One-time setup (PowerShell):

```powershell
az storage container create -n app-request-logs --account-name m4dlogs --auth-mode login
$account = az storage account show -n m4dlogs -g m4d-Web --query id -o tsv
az role assignment create `
  --assignee-object-id (az webapp identity show -n msc4dnc -g m4d-Web --query principalId -o tsv) `
  --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor" `
  --scope "$account/blobServices/default/containers/app-request-logs"
# Add the rule to the existing policy rather than replacing it:
az storage account management-policy show --account-name m4dlogs -g m4d-Web > local/policy.json
#   ...append a rule like the HTTP-log one, with prefixMatch ["app-request-logs/"], then:
az storage account management-policy create --account-name m4dlogs -g m4d-Web --policy "@local/policy.json"
az appconfig kv set -n music4dance --key RequestLog:Enabled --value true --yes
az webapp restart -n msc4dnc -g m4d-Web
```

The startup console shows `[RequestLog] Enabled: ...`, and blobs show up within a couple of
minutes. A missing role shows up as the dropped-requests Warning in the app log.

**Format.** One append blob per hour at
`app-request-logs/<prefix>/y=YYYY/m=MM/d=DD/h=HH/m=00/PT1H.json` (UTC, the same layout as the
HTTP request log), with one flat JSON record per line (no nested `properties` string):

| Field | Meaning |
| --- | --- |
| `time` | Request start, UTC |
| `CIp` | The visitor: `CF-Connecting-IP` when relayed by Cloudflare, else the connecting address |
| `RelayIp` | The address that handed the request to App Service: the Cloudflare edge when proxied |
| `CsHost`, `CsMethod`, `CsUriStem`, `CsUriQuery` | As in the HTTP request log (query without the `?`) |
| `ScStatus`, `TimeTaken` | Status after the exception handler; `500` for an unhandled exception even if the response had started (the connection is aborted); `499` when the client gave up. Time in the app (ms; the platform's figure also includes the front end) |
| `ScBytes`, `CsBytes` | Response body bytes written (after compression; `0` for HEAD, whose body Kestrel discards), and the request's `Content-Length` |
| `UserAgent`, `Referer`, `Protocol` | As sent |
| `Cookie` | Cookie **names only** (`_ga=; music4dance=`), never values |

**Cost.** A few MB of blob writes an hour at current traffic, in about 60 appends: pennies a month
at 30 days' retention. In memory it holds at most one flush interval's worth of records.

**Privacy.** The same rules as the HTTP request log: full client IPs and query strings, private
account, Entra ID reads only, logs kept under `local/`. Cookie values are left out, so no
session tickets are stored.

Reading: `python -I .claude/skills/analyze-traffic/fetch_http_logs.py --source app` downloads
only the `msc4dnc/` blobs (`--site` picks another prefix, so local-dev `local/` blobs never mix
in) to `local/app-request-logs/msc4dnc/`, and `analyze_http_logs.py` reads either format. Keep the two sources
in separate directories.

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
- 2026-11-15: App request log added (`RequestLogMiddleware`, container `app-request-logs`), off
  until `RequestLog:Enabled`. Behind Cloudflare the HTTP request log's `CIp` is the edge server;
  this log records the visitor.

## Related

- [runbooks/read-production-logs](../runbooks/read-production-logs.md)
- [runbooks/capture-memory-diagnostics](../runbooks/capture-memory-diagnostics.md)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md)
- [individual-artists](../songs/individual-artists.md): artist-index memory measurements, which used these tools
