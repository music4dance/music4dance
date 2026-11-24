# Logging and Runtime Diagnostics

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-23
**Code:** `m4d/Configuration/M4dApplicationExtensions.cs` (logging setup),
`m4d/Services/Diagnostics/` (`GcDiagnostics`, `GcSnapshot`, `DumpResult`),
`m4d/Controllers/AdminController.cs`, `m4d/Views/Admin/Diagnostics.cshtml`,
`m4d/Configuration/RequestLogSetup.cs` and `m4d/Middleware/RequestLogFieldsMiddleware.cs` (app
request log), `m4d/Configuration/AppLogUploadSetup.cs` (app log upload),
`m4d/Services/LogFileUploader.cs` (copies both to blob storage)

Where music4dance's application logs go, and what runtime memory diagnostics the app exposes.
Security and traffic diagnostics on the same admin page are covered in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md). Service-health reporting is
covered in [service-resilience](../infrastructure/service-resilience.md).

## Application logs

**Providers.** Logging is reset and configured in `M4dApplicationExtensions`, with two providers:
`AddConsole()` and `AddAzureWebAppDiagnostics()` (in `AddM4dLogging`). Log levels come from configuration
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
- On `/home` it's a **rolling window**: free, but not long-term, not queryable, and without
  alerting. The [App log upload](#app-log-upload) copies it to blob storage for 30 days.
- The file logger writes one `diagnostics-YYYYMMDD.txt` per UTC day and deletes the oldest past
  `RetainedFileCountLimit`. Its default of 2 kept only about a day and a half (on 2026-11-23,
  11-19 to 11-22 were gone), so `AddM4dLogging` keeps 7 (`AzureFileLogRetainedFiles`). Past its
  `FileSizeLimit` it **silently drops the rest of the day**. `AddM4dLogging` raises the limit
  from the 10 MB default to 50 MB (`AzureFileLogSizeLimitBytes`), because a Search throttling
  burst on 2026-11-18 filled 10 MB in about 8 minutes and hid the next 4¾ hours. Container
  stdout keeps logging past the cap until it rotates.
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

Queryable options, and what to do about those resources, are in
[plans/log-persistence-options](../plans/log-persistence-options.md).

## App log upload

App Service's "Application Logging (Blob)" isn't offered on Linux, so the app copies its own app
log. When `AppLog:UploadContainer` is set, `AppLogUploadSetup` registers a second
`LogFileUploader`, the same uploader as the [app request log](#app-request-log)'s (described
there: 5 minutes after start, then hourly, whole lines appended to one append blob per file,
resuming from the blobs' lengths, failures logged and retried). It copies
`/home/LogFiles/Application/diagnostics-*.txt` to container `app-diagnostic-logs` in `m4dlogs`,
under the same file names, and the lifecycle rule deletes them after 30 days.

- **It never deletes files.** The file logger deletes its own past `RetainedFileCountLimit` (7),
  and the `az webapp log download` the triage uses still reads `/home`. So 7 days is the buffer
  while uploads fail.
- **What can be lost:** the last hour before the file logger drops a day (it won't, while the
  count is 7 and uploads work), and anything past the 50 MB daily cap, which is never written.
- **Volume:** a normal day is under 1 MB; a Search throttling day can reach the 50 MB cap. Either
  way, pennies.
- **One instance writes the file.** The file names have no instance tag, so two instances on
  one `/home` would both append to the same day file; the uploader copies whatever the file
  holds. Production runs one instance.
- **Reading it:** `python -I .claude/skills/analyze-logs/fetch_logs.py app-log --days 7` merges
  the blobs into `local/logs/msc4dnc/LogFiles/Application/`, beside the `app-service` download.

**Settings:**

| Key | Default | Notes |
| --- | --- | --- |
| `AppLog:UploadContainer` | none | `https://m4dlogs.blob.core.windows.net/app-diagnostic-logs`. Unset, nothing is copied. Read at startup |
| `AppLog:Directory` | `$HOME/LogFiles/Application` | Where the file logger writes on App Service; only set it to test locally |

A bad value turns the upload off with `[AppLog] WARNING: upload disabled, ...` instead of failing
startup. When it's on, startup prints `[AppLog] <directory>/diagnostics-* copied hourly to ...`.

**One-time setup** (PowerShell, as an Owner of `m4d-Web`). The key has the `Production` label for
the same reason as the request log's (see Environments under
[App request log](#app-request-log)); it takes effect at the next restart or deploy:

```powershell
az storage container-rm create --storage-account m4dlogs -g m4d-Web -n app-diagnostic-logs
$app = az webapp identity show -n msc4dnc -g m4d-Web --query principalId -o tsv
$account = az storage account show -n m4dlogs -g m4d-Web --query id -o tsv
az role assignment create --assignee-object-id $app --assignee-principal-type ServicePrincipal `
  --role "Storage Blob Data Contributor" `
  --scope "$account/blobServices/default/containers/app-diagnostic-logs"
az storage account management-policy update --account-name m4dlogs -g m4d-Web `
  --add policy.rules[0].definition.filters.prefixMatch app-diagnostic-logs/
az appconfig kv set -n music4dance --label Production --key AppLog:UploadContainer `
  --value https://m4dlogs.blob.core.windows.net/app-diagnostic-logs --yes
```

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
(`AddW3CLogging` / `UseW3CLogging`), set up in `RequestLogSetup`, to `/home`, and copied every hour
to blob storage, which keeps 30 days. It sits right after
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

**Format.** Daily files `requests-<instance>-YYYYMMDD.NNNN.txt`: UTC dates, a new `NNNN` at each
restart and at `FileSizeLimitMB`, and `<instance>` the first 8 characters of App Service's
`WEBSITE_INSTANCE_ID` (else the machine name). The logger restarts numbering at `.0000` each day in
every process, so without the instance tag two instances sharing `/home` would write the same file. A `#Fields:` header names the space-separated columns: `date time cs-method
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
- It deletes the oldest of its own files (its instance's prefix) past `RetainedFileCountLimit`. That
  counts files, not days: every restart and size rollover adds one.

**Where the files go.** The logger writes to `/home/LogFiles/requests`, and `LogFileUploader`
copies them to container `app-request-logs` in `m4dlogs` every hour:

- **Why `/home` and not straight to storage.** The logger writes and flushes every line, and a slow
  write blocks requests (above). An Azure Files share mounted into the app would cost about $20–30 a
  month in transactions at current traffic (one write per request plus metadata calls) and add a
  dependency that can stall the site. `/home` costs nothing and adds no new stall risk: the site
  already runs from it.
- **The upload** runs 5 minutes after start, then hourly, in the app (a `BackgroundService`, not in
  the request pipeline). Each pass lists the container once, then appends to each file's append
  blob the whole lines the file has gained since, in chunks of up to 1 MB, conditioned on the blob's
  current length so a retry can't duplicate lines. It keeps no state: after a restart it resumes
  from the blobs' lengths. It takes every `requests-*.txt` in the directory, so files a previous
  instance left behind (after a move to another VM) are copied too; two uploaders on one file are
  harmless, since the bytes are the same and a stale append is refused. At about 100 MB a day
  that's around a hundred appends a day: pennies.
- **Failures** never escape the pass (an exception out of a `BackgroundService` would stop the
  site): a failed file is a `[RequestLog] Couldn't upload` warning, a failed pass (say, a missing
  role or container) an `Upload pass failed` warning, and both are retried next pass.
- **Cleanup:** once a file is fully copied and hasn't been written for 2 days, the uploader deletes
  it, so `/home` normally holds about 2 days (about 200 MB). A file that isn't copied is never
  deleted by the uploader.
- **What can be lost:** nothing while uploads work. While they fail, files pile up until the
  logger's `RetainedFileCountLimit` (30 files) deletes the oldest. That's 30 restarts or size
  rollovers, so a buffer of anywhere from a few days of frequent deploys to about a month.
- **Space on `/home`:** the cap is 30 files of up to 100 MB, at most 3 GB of the plan's 11 GB (2 GB
  used, 2026-11-16), reached only after a long upload outage.
- **Retention in storage:** the account's lifecycle rule `delete-http-logs-after-30-days` also
  matches the `app-request-logs/` prefix.

**Settings:**

| Key | Default | Notes |
| --- | --- | --- |
| `RequestLog:Enabled` | `false` | Read at startup, so restart after changing it |
| `RequestLog:Directory` | none | An absolute path; the logger creates it. `/home/LogFiles/requests` in production |
| `RequestLog:UploadContainer` | none | The container URL, `https://m4dlogs.blob.core.windows.net/app-request-logs`. Unset, the files stay only in the directory |
| `RequestLog:RetainedFileCountLimit` | `30` | 1–1000. Files, not days (each restart and size rollover starts one). With uploads working the uploader cleans up first, so this is the buffer while uploads fail |
| `RequestLog:FileSizeLimitMB` | `100` | 1–2000. About a day's traffic, so a busy day rolls into a second file. This times the count above is the most `/home` can hold |

The settings are read once, at startup. A bad value, including one that isn't a number or a
boolean, turns the log off with a `[RequestLog] WARNING: disabled, ...` line instead of failing
startup. When it's on, startup shows `[RequestLog] Enabled: <directory>/<file prefix>*, ...`, ending
in where it's copied. If App Configuration didn't load at startup, the log can't know it should be
on: startup prints `[RequestLog] WARNING: off, because App Configuration didn't load at startup`,
and if `AppConfigurationRecoveryService` later loads it with the log enabled, its recovery warning
and email say to restart.

**One-time setup** (PowerShell, as an Owner of `m4d-Web`). Nothing here restarts the app, and nothing
takes effect until code that reads `RequestLog:*` is running; the next deploy or restart turns it on:

```powershell
# The container, and write access to it (only it) for the app's managed identity
az storage container-rm create --storage-account m4dlogs -g m4d-Web -n app-request-logs
$app = az webapp identity show -n msc4dnc -g m4d-Web --query principalId -o tsv
$account = az storage account show -n m4dlogs -g m4d-Web --query id -o tsv
az role assignment create --assignee-object-id $app --assignee-principal-type ServicePrincipal `
  --role "Storage Blob Data Contributor" `
  --scope "$account/blobServices/default/containers/app-request-logs"
# Keep 30 days, with the same lifecycle rule as the HTTP log
az storage account management-policy update --account-name m4dlogs -g m4d-Web `
  --add policy.rules[0].definition.filters.prefixMatch app-request-logs/
# Turn it on, for production only (see Environments below)
az appconfig kv set -n music4dance --label Production --key RequestLog:Directory `
  --value /home/LogFiles/requests --yes
az appconfig kv set -n music4dance --label Production --key RequestLog:UploadContainer `
  --value https://m4dlogs.blob.core.windows.net/app-request-logs --yes
az appconfig kv set -n music4dance --label Production --key RequestLog:Enabled --value true --yes
```

**Environments.** The keys carry the `Production` label, which only `msc4dnc` loads
(`AppConfigurationStartup` selects unlabeled keys, then the environment's label). So:

- **`m4d-test`** (`Staging`) shares the store but doesn't see them, so the log is off there. Don't
  make the keys unlabeled: test would then copy its own `requests-*.txt` files into the same
  container under the same names, mixing its lines into production's blobs. To turn it on in test,
  add `Staging`-labeled keys with a separate container and a role for `m4d-test`'s identity. Its F1
  plan has a 1 GB `/home` and no Always On, so keep `RetainedFileCountLimit` and `FileSizeLimitMB`
  low, and expect uploads only while the app is awake.
- **Local development and contributor environments** run as `Development`, which doesn't load App
  Configuration, and `appsettings*.json` doesn't set `RequestLog:Enabled`, so it's off. Set
  `RequestLog:Enabled` and `RequestLog:Directory` in user secrets to try it; leave
  `UploadContainer` unset.

A new role assignment can take a few minutes to apply; the first pass is 5 minutes after start,
and a pass that fails is simply retried an hour later. To check, look for `[RequestLog] Uploaded`
in the app log, or list the container.

**Privacy.** The same rules as the HTTP request log: full client IPs and query strings, read only
by the site's owner, and kept under `local/` once downloaded. Cookie values are never recorded.

**Reading:** `python -I .claude/skills/analyze-logs/fetch_logs.py requests` downloads
from the container (same `Storage Blob Data Reader` role as the HTTP request log) to
`local/logs/requests/`, fetching only the new bytes of a file it already has. The same script
puts the App Service log download (`app-service`, which also carries the `/home` copy of these
files) and the platform HTTP log (`http`) in the same `local/logs/` tree, and `status` shows what's
already there, so the `analyze-logs` and `analyze-traffic` skills share one copy; `--from home`
reads `/home` through Kudu instead, for the last hour or so that hasn't been copied yet, and `analyze_http_logs.py` reads either source (one per
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

- **Log query and alerting:**
  [plans/log-persistence-options](../plans/log-persistence-options.md) (Log Analytics with a hard
  cap, or a tuned App Insights).
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
  response bytes for much less code. It writes to `/home` and `RequestLogUploader` copies it to
  blob storage hourly; writing to a mounted Azure Files share was considered and dropped for its
  transaction cost and stall risk.
- 2026-11-23: App log upload. `RequestLogUploader` became `LogFileUploader`, one per log, and
  copies `diagnostics-*.txt` to `app-diagnostic-logs` when `AppLog:UploadContainer` is set. The
  file logger keeps 7 day files instead of 2. The triage had found 11-19 to 11-22 gone.

## Related

- [runbooks/read-production-logs](../runbooks/read-production-logs.md)
- [runbooks/capture-memory-diagnostics](../runbooks/capture-memory-diagnostics.md)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md)
- [individual-artists](../songs/individual-artists.md): artist-index memory measurements, which used these tools
