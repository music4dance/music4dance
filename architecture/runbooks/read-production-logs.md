# Read Production Logs

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-11-23

## When to use

You need to see what the app logged: warnings or errors after an incident, the startup
service-health report after a deploy, antiforgery or rate-limit warnings, and so on. How logging
is configured is described in [logging-and-diagnostics](../observability/logging-and-diagnostics.md).

## Prerequisites / access

- Azure CLI signed in to the subscription, with access to resource group `m4d-Web`; or Kudu
  (SCM) access to the app. The subscription is in the `music4dance` tenant: use
  `az login --tenant 77fd022e-b4cb-4681-aa1d-c4f8ec6bec90`. A bare `az login` fails with
  AADSTS50076 because both tenants on the account require MFA, and only `--tenant` prompts for
  it. If the Windows account picker opens empty, add `--use-device-code` (or turn off the broker
  with `az config set core.enable_broker_on_windows=false`).
- `az webapp log download` goes through Kudu, which shares the app's VM: while the app is out of
  memory it returns `503` from `/dump`. Restart the app, wait for it to come back, then download.
- The `analyze-logs` Claude Code skill (`.claude/skills/analyze-logs/`) automates steps 2 and 3:
  it downloads the logs, merges them into `local/logs/msc4dnc/`, and groups them into
  signatures, with the matching 5xx requests from the app request log. Its `fetch_logs.py` is the
  one downloader for every log in this runbook. It keeps them all under `local/logs/` (`msc4dnc/`,
  `m4d-test/`, `requests/`, `http/`), and `fetch_logs.py status` shows what's already there.
  `fetch_logs.py app-log` adds the app log's 30-day copy in blob storage, for days `/home` no
  longer has.
- Know what's actually persisted: **Warning and above** from `ILogger`, plus container stdout and
  stderr, which includes the console startup output. Information-level logs are *not* persisted.
  Watch them live with `az webapp log tail`, or temporarily lower the level.

## Steps

1. **Live:**
   ```bash
   az webapp log tail --name msc4dnc --resource-group m4d-Web     # or m4d-test
   ```
   This is a live tap only. Anything written while no one is connected is lost from this view.
2. **After the fact**, either:
   - Download everything (wraps `az webapp log download` and merges the zip into
     `local/logs/msc4dnc/`, which is gitignored; earlier days stay):
     ```bash
     python -I .claude/skills/analyze-logs/fetch_logs.py app-service
     ```
     Then grep, e.g. `grep -rn "Antiforgery validation failed" local/logs/msc4dnc/`.
   - Or browse in Kudu: `https://msc4dnc.scm.azurewebsites.net/api/vfs/LogFiles/Application/`
     (or the Kudu console at `/home/LogFiles/Application/`).
3. **Useful searches:**
   - `=== music4dance.net Service Health Report ===` for the startup report after a deploy or
     restart
   - `is now unavailable` and `remains unavailable` for service-health transitions (see
     [service-resilience](../infrastructure/service-resilience.md))
   - `RateLimit: EXCEEDED` and `GLOBAL rate limit exceeded` for identity-endpoint limits (see
     [bot-and-abuse-defense](../security/bot-and-abuse-defense.md))
   - `Unhandled exception for request:` for unhandled exceptions, with the URL
   - `Antiforgery validation failed` for the usage-log and suggestion endpoints
4. **Check the day's file wasn't capped.** If a `diagnostics-YYYYMMDD.txt` stops well before
   midnight UTC and is right at its size limit (10 MB before 2026-11-19, 50 MB since), the file
   logger dropped the rest of that day. The container log (`*_default_docker.log`) still has the
   `warn:`/`fail:` entries until it rotates, so grep there for the missing hours.

## HTTP request logs

The app logs above don't record ordinary requests. For per-request data (who is calling, which
URLs, status, response time), read the hourly blobs in storage account `m4dlogs`. Setup, format
and privacy notes are in
[logging-and-diagnostics § HTTP request logs](../observability/logging-and-diagnostics.md#http-request-logs).
The download in step 2 doesn't include them, and there's no `http` folder in Kudu on Linux.
Behind Cloudflare these records show the edge server as the client; the app request log has the
visitor's IP
([logging-and-diagnostics § App request log](../observability/logging-and-diagnostics.md#app-request-log)).
Fetch it with `fetch_logs.py requests` (into `local/logs/requests/`), and the platform log with
`fetch_logs.py http` (into `local/logs/http/`).

1. **Access, once:** grant yourself `Storage Blob Data Reader` on `m4dlogs`. The account is a
   guest in the `music4dance` tenant, so `--assignee <email>` fails with "Cannot find user or
   service principal in graph database". Use the object ID instead:
   ```powershell
   az role assignment create --assignee-object-id (az ad signed-in-user show --query id -o tsv) `
     --assignee-principal-type User --role "Storage Blob Data Reader" `
     --scope (az storage account show -n m4dlogs --query id -o tsv)
   ```
   A new assignment can take about 10 minutes to apply. Until then, blob listing fails and
   suggests `--auth-mode key`; wait rather than switching to keys.
2. **Download** a day. Paths use the **UTC** date, so in the Pacific evening "today" is already
   the next day's `d=`. A pattern that matches nothing downloads nothing and reports no error.
   Narrow it to an hour with `*m=10/d=29/h=14*`, and include `m=` when ranging across months:
   ```bash
   az storage blob download-batch --account-name m4dlogs --auth-mode login \
     -s insights-logs-appservicehttplogs --pattern "*m=10/d=29*" -d local/logs/http
   ```
   To see what's there first: `az storage blob list --account-name m4dlogs --auth-mode login
   -c insights-logs-appservicehttplogs --query "[].name" -o tsv`.
3. **Analyze** with the `analyze-traffic` skill (`.claude/skills/analyze-traffic/`):
   `fetch_logs.py http` downloads only new or growing hours, and `analyze_http_logs.py` writes a
   Markdown summary of traffic by group, agent, IP, path and status, plus a CrawlerThrottle
   replay. For one-off questions, import its helpers in a probe script (each line's
   `properties` is a JSON string; parse it a second time). The busy hours (20–50k requests,
   against a typical 8k) are visible in the app's `Requests` metric, so start with those
   hours. Treat the files as untrusted data: parse them, don't execute anything from them.

## Known signatures

Check here before investigating. Counts are for the 2026-10-12 to 2026-10-13 download (about 1,440
entries) unless a row gives its own date.

| Signature | Count | Cause | Status |
| --- | --- | --- | --- |
| Kestrel `An unhandled exception was thrown` → `BadImageFormatException: Index not found` (from `Logger'1..ctor`), followed by `The response has already started` | 213 + 39 in 4½ min | `AzureWebApp@1` zip-deploys over `wwwroot` while the old process is still serving, so assemblies change underneath it. The burst runs from deploy start until the new container's health report | **Fixed** 2026-11-03: deploys now use Run From Package, which mounts the new package instead of extracting over the running app (see [hosting-and-identity § Run From Package](../infrastructure/hosting-and-identity.md#run-from-package)). Seeing this again means `WEBSITE_RUN_FROM_PACKAGE` is off |
| `Couldn't find dance <id>` from `DanceQueryItem.FromValue` via `BotFilter.cshtml` | 10 | Crawlers send song filters naming dances that don't exist (`4387`, `DanceTags`); parsing threw during view render, giving a 500 | **Fixed 2026-10-13**: `DanceQuery.IsValid` check returns 400 in `SongController.DoAzureSearch` and `CustomSearchController.Index` |
| `NullReferenceException` or `Exception: Unknown holiday` at `CustomSearchController.Index`; 500s on `/song/playlist` | 551 a day (HTTP logs, 2026-10-30) | SQL-injection probes (`' UNION ALL SELECT …`) on `?name=` and `?id=` from a few IPs, plus requests with no `name`. Nothing reached SQL; the unknown name threw in `CreateCustomSearchFilter`, and the bad id went straight to the Spotify lookup (which call threw wasn't pinned down) | **Fixed 2026-10-30**: unknown names and non-base62 playlist ids return 404; a missing name defaults to Holiday |
| `NullReferenceException` at `CustomSearchController.Index` line 95, each after a Search `400 Bad Request` | 4 (11-17 to 11-18); 4 more (11-18 to 11-19) | An unknown `dance` (`/CustomSearch?name=halloween&dance=Slow`): `CreateCustomSearchFilter` built OData with nothing before ` and (`, Search rejected it, then `DanceStats.FromName` returned null | **Fixed 2026-11-18**: `SongFilter.IsCustomSearchDance` returns 404 for an unknown dance (#408). Production was still on #407 on 11-19. The request log shows 6 more `500`s on `/CustomSearch` up to 2026-11-20 17:26 and none since the 11-20 18:06 deploy |
| `System.Exception: Invalid value format: '` from `DanceQueryItem.FromValue` via `SongController.AzureSearch` (container log only) | 1 (2026-11-21 01:37) | A probe sent `/song/search?dances=%27`. `AzureSearch` counts the query's dances to pick a sort order before `DoAzureSearch` runs its `DanceQuery.IsValid` check, and parsing the malformed value threw, giving a 500 | **Fixed 2026-11-23**: `AzureSearch` checks `DanceQuery.IsValid` as soon as it sets `Filter.Dances` and returns 400 |
| `AuthenticationFailureException: An error was encountered while handling the remote login. ---> Correlation failed.` on `/signin-google` (container log only) | 1 (2026-11-22 02:00) | The OAuth callback arrived without a valid correlation cookie: the sign-in took too long, the browser blocked or dropped the cookie, or the callback URL was reopened. With no `OnRemoteFailure` handler it was unhandled, giving a 500 | **Fixed 2026-11-23**: every provider's `OnRemoteFailure` logs `External sign-in with <scheme> failed: …` as a Warning and redirects to `/Identity/Account/Login?reason=remote-failure`, which asks the user to try again. A few of those Warnings a week is noise |
| `InvalidOperationException: This request does not have a Content-Type header. Forms are available…` from `DanceMusicController.GetFilterFromContext` via `ContentController.OnActionExecutionAsync` | 1 (2026-11-18 23:42, container log only) | A POST with no `Content-Type` (or a non-form body) to any `ContentController` action: with no `?filter=`, it read `Request.Form`, which throws, giving a 500 | **Fixed 2026-11-19**: `ReadFilterString` reads the form only when `HasFormContentType`, otherwise it uses the default filter |
| Only two `diagnostics-YYYYMMDD.txt` files on disk | 2026-11-23 download: only 11-23 and 11-24; 11-19 to 11-22 gone | The App Service file logger keeps `RetainedFileCountLimit` files, default **2**, and `AddM4dLogging` doesn't change it. So the app log covers about a day and a half, and a triage that starts later than that has only the request log and whatever container logs haven't rotated. The 11-20 Search slowdown and the 11-21/11-22 `500`s could only be traced through the container log | **Fixed 2026-11-23**: `AddM4dLogging` keeps 7 files, and with `AppLog:UploadContainer` set the app log is copied to `app-diagnostic-logs` hourly and kept 30 days; fetch it with `fetch_logs.py app-log` ([logging-and-diagnostics § App log upload](../observability/logging-and-diagnostics.md#app-log-upload)) |
| `/api/health/status` is a large share of requests | ~80,000 of 464,000 (2026-11-19 to 11-24, request log), 10,000 an hour at the 11-20 22:00 peak; ~9% on 11-22 and 11-23 (5,600 of them ShapBot's renderer) | Every page view fetches it once on load (`useServiceHealth.startPolling`), although the page already has the status from `MenuContext`, then hourly. 42,000 of them came from the Chrome/48 crawler's rendering farm (`Chrome/99.0.4844.51` on 116.179.x), gone since the 11-21 edge rule. The endpoint is cheap (in-memory status), so this is request volume, not Search load | **Open**: skip the initial fetch when `MenuContext` has fresh status (unless pages are edge-cached and need it), or have the crawler pre-render path not run the poller |
| `diagnostics-YYYYMMDD.txt` stops mid-day at 10,485,760+ bytes | 2026-11-18: nothing after 19:14:39 UTC | The App Service file logger's `FileSizeLimit` (default 10 MB) silently drops everything past it until the next UTC day. The two Search throttling bursts filled it in ~8 minutes: 6.5 MB of `503` header dumps and 2.6 MB of `Response … took Ns` lines. The container log still had the lost 4¾ hours, including the `Content-Type` 500 above | **Fixed 2026-11-19**: `AddM4dLogging` raises the limit to 50 MB (2 files kept, ~100 MB of `/home`). A burst like 11-18's writes ~1.6 MB a minute, so the Search circuit breaker (next row) is what keeps an outage from filling even 50 MB. Don't raise the `Azure.Core` level to cut volume: that also drops the `404`/`400` entries above |
| `Azure.Core` `404 Not Found` with a `Sync-Token` header, every ~5 min | ~815 | App Configuration refresh polls `Configuration:Sentinel` with label `Production`, which doesn't exist (only unlabeled and `Staging` do). Also means bumping a sentinel never refreshes production | **Fixed 2026-10-14**: the refresh now watches the unlabeled sentinel (`AppConfigurationStartup`) |
| `Azure.Core` `404 Not Found` with `request-id`/`elapsed-time` headers | ~100 | Azure AI Search: `FindSong` for IDs not in the index. Confirmed 2026-11-18 (453 in 2 days) against the app request log: they line up with `/song/details/<id>` 301s, the redirect for a merged song's old ID. 2026-11-18 to 11-19: 1,089, all Search; ~15 an hour normally, 60–120 an hour while the residential-proxy scraper ran, ~35 an hour all of 11-19 | Noise; the rate follows crawler traffic |
| `Azure.Core` `400 Bad Request` with `request-id`/`elapsed-time` headers, in pairs | 70 | Azure AI Search rejects a request. 2026-11-18 (198 in 2 days), matched against the app request log: nearly all are `` `Artist:(…) `` Lucene searches on `/Song` and `/song/filtersearch` whose value has Lucene syntax in it (`IAMSU!`, an unbalanced `)`) or was cut off at an unencoded `&` (`` `Artist:(Coco ``). `SongIndex.Search` catches the failure and shows 0 results with a 200, and the error body only goes to `Trace.WriteLine` | **Fixed 2026-11-18**: `KeywordQuery.AzureSearch` escapes every Lucene special character in the user's text (and lowercases `AND`/`OR`/`NOT`) before `SongIndex.Search` and `StreamAll` send it; field values escape `\`, `(` and `)` so parentheses round-trip; the artist page's search links are URL-encoded. **Open**: log the `RequestFailedException` message, and stop `DoSearch` retrying a 400 without `select` (the cause of the pairs) ([#415](https://github.com/music4dance/music4dance/issues/415)) |
| `Azure.Core` `503 Service Unavailable` with `request-id`/`elapsed-time` headers, thousands a minute; with `Search operation failed due to unavailable…`, unhandled `InvalidOperationException: Azure Search service is unavailable` from `SongIndex.DoSearch`, and `Service 'SearchService' is now unavailable` (which emails) | 12,024 503s in 18:10–18:14 UTC on 2026-11-18, then another burst 19:12–19:16 (its last minutes are only in the container log; the file had hit its size cap). None on 11-19 to 15:43 | Azure AI Search throttling (Basic, 1 replica; the portal's *Throttled search queries %* hit 99% and 95%). Each time it was a burst of the residential-proxy scraper ([bot-load § 1.1](../plans/bot-load-and-public-page-rendering.md)): `Chrome/153.0.0.0` on Windows, no cookies, no referrer, ~10,800 IPs with at most 3 requests each, ~4,000 requests a minute (median 43), mostly `/song/album` and `/song/artist`. It ran all day from 16:00 UTC at 5–8k requests an hour. Search slows to ~45 s before it starts returning 503s; the container log rotates out within minutes, so use the app request log | **Partly fixed 2026-11-18**: album pages returned 404 and artist pages 500 during the outage; both now check `IsSearchAvailable` and return 503. **Fixed 2026-11-19** (the log flood and the flapping): `SearchCircuitBreaker` (one per Search service) fails Search queries fast for 30 s after a `503`/`429`, without sending or logging them, then lets one probe through; document writes bypass it and keep the SDK's retries ([service-resilience § Degraded behavior](../infrastructure/service-resilience.md#degraded-behavior)). Expect one wave of `503`s from requests already in flight, then `Search circuit breaker opened` / `stays open: … N requests failed fast` / `closed` Warnings instead of thousands of `503`s. **Open**: edge bot management (Bot Fight Mode, on since 11-07, doesn't stop it), or an origin concurrency limit |
| Slow `/song/artist` requests ending in `500` after 100–166 s, with `503`s on `/song/album`, `/song/artist` and `/api/health/status`, in 23:35–23:39 UTC | 2026-11-20: 7 `500`s, 764 `503`s (request log; the app log for that day had already rotated out) | A Search slowdown under ~400 requests a minute, mostly from two crawler populations: `Chrome/48.0.2564.116` on 111.225.x and 119.249.x (`/song/album`, `/song/artist`), and `Chrome/99.0.4844.51` on 116.179.x, a Baiduspider range, fetching `/api/health/status` with `/song/details` referrers (a rendering crawler running the page's script). Requests averaged 20 s in 23:30–23:39. The `503`s are the circuit breaker working; the `500`s were artist requests already in flight that timed out before it opened. This is the Chrome/48 album crawler and its rendering farm ([bot-load](../plans/bot-load-and-public-page-rendering.md)); a Cloudflare custom rule has blocked that user agent since 2026-11-21 00:31 UTC, and neither has been seen since 11-21 | **Open**: the in-flight timeouts still surface as `500`; find the exception the next time this happens while the app log still has it, and map it to `503` |
| `Search circuit breaker opened for <service>` / `Search circuit breaker for <service> stays open` / `… closed` from `SearchCircuitBreaker` | New 2026-11-19 | Azure AI Search returned `503`/`429`; see the row above | Expected during throttling. `stays open` for more than a few minutes means a sustained Search outage: check the Search resource's metrics |
| SQL `error occurred during the login process`, App Configuration `A refresh operation failed`, at the same time as a Search throttling burst | 1 SQL, 2 App Configuration (2026-11-18 19:13) | A side effect of the same burst on the B1 container: the request log shows ~3,700 requests in that minute, averaging 96 s each. No restart | Recheck if it happens without a burst |
| `AdmAuthentication`: `Refresh token expired during create` | 1 (2026-11-18) | A user's stored refresh token had expired | Noise |
| `Azure.Core` `Response … took Ns` | 57; 23,502 (11-18 to 11-19) | Slow Azure responses (App Configuration, Search). On 11-18, 23,488 of them came in the two Search throttling bursts (18:xx and 19:xx, average 39 s, max 82 s). Outside the bursts: 3 on 11-18 and a cluster of 11 at 2026-11-19 11:23:56 (4 to 15 s, all finishing within one second, no 503s after). 2026-11-23 to 11-24: 16, in two clusters (11-23 11:55:56, two Search requests at 8 and 14 s; 11-24 00:00:01–03, six at 5 to 19 s), no 503s after | Watch; a burst of these just before `503`s is Search throttling (row below). See the App Configuration timeout work |
| `LuckyPennySoftware.AutoMapper.License`: license expired | 2 per start | AutoMapper's commercial license key (`Authentication:AutoMapper:Key`) expired around 2026-09-23 | **Fixed 2026-10-14**: renewed through 2027-10-14; `LicenseKeyCredential` now emails 30 days before expiry ([runbooks/renew-automapper-license](renew-automapper-license.md)) |
| `Spotify request …/playlists/<id> failed with 404` | 38 in one 9-min run | A batch reads playlists that are gone. The `37i9dQZF1D…` IDs are Spotify-owned editorial playlists, which the Web API stopped serving to new apps in Nov 2024. On 2026-11-17 and 11-18 it's ~38 a day, all at 08:00 UTC, on `v1/users/<owner>/…` (most `spotify`) | **Fixed 2026-11-18**: the batch soft-deletes a `SongsFromSpotify` playlist after 7 days of 404s ([playlist-management § Playlists that disappear from Spotify](../music-services/playlist-management.md#playlists-that-disappear-from-spotify)). Expect these until then, plus a `marked SongsFromSpotify playlist … deleted` Warning for each; an `Error` saying more than usual weren't found may mean something on our side, and those are marked after 21 days unless they load again. Still 76 in the 08:00 runs on 11-18 and 11-19, because #408 wasn't deployed yet. It went out 2026-11-20 18:06, and there were 38 on 11-23 at 08:01–08:09, so the first soft-deletes are due around 11-27. A single `failed with 500: An unexpected error occurred` in the same run (11-18 08:08) is Spotify-side: noise |
| `Antiforgery validation failed for suggestion request`, once per keystroke | 90 | No cookie (`HasAntiforgeryCookie=False`), a cookie/token mismatch, or a token for a different user after signing in from another tab. Each keystroke logs again | **Open**: see the 4xx triage log; consider re-fetching the token on failure and logging once per page |
| `Antiforgery validation failed for usage log batch` | 6 | Same causes, from the usage beacon | Noise unless it grows |
| `ArtistsCoverage (SongIndexExperimental) failed`: index `songs-experimental` not found | 2 | Admin page run against an index that doesn't exist in production | Noise |
| `HttpsRedirectionMiddleware`: `Failed to determine the https port` | 1 per start | App Service terminates TLS in front of the container | Noise |
| `Out of memory.` then `startup.sh: … Aborted (core dumped) dotnet m4d.dll` (platform log: `exit code: 134`), preceded by Kestrel `heartbeat … thread pool starvation` and `OutOfMemoryException` in `_jsonCamelCase`, `ScriptJson.Escape`, `SongIndex.FindSong`/`DoSearch` | Several restarts a day from 2026-10-22 (10-23 to 10-25 download) | Peak working set pinned at ~1.25 GB, the most the B1 container allows; it was 700 MB–1 GB before. It began within a day of the platform moving to base image `dotnetcore:10.0_20261022.3`, with no app deploy and lower traffic. Each restart takes up to ~15 min to come back, often on a new VM | **Open**: scale up the plan, or cap the GC heap; take a memory dump near the ceiling |
| SQL `error occurred during the login process` / `pre-login handshake` / `Timeout … obtaining a connection from the pool`; Search `Retry failed after <n> tries` | ~250 SQL, ~800 Search (10-24 to 10-25) | A symptom of the memory and thread-pool starvation above, not a dependency outage: SQL, Search and App Configuration metrics were normal at the same times | Recheck after the OOM is fixed |
| `Suspicious returnUrl detected from <ip>` on `/identity/account/login` | 4 | Open-redirect probes from scanners | Noise |
| `RegisterModel` / `LoginModel`: `Non-local return URL detected` | 4 (11-17 to 11-18); 6 Register + 1 Login (11-18 to 11-19) | The same open-redirect probes, on `/identity/account/register` and `/identity/account/login` | Noise |
| SQL `Execution Timeout Expired` (65 s) on the `AspNetUserRoles` role lookup | 1, 2026-11-17 19:13, 2 min after a deploy's start | A cold start: migrations and cache loads were running. No repeat | Watch; investigate if it recurs outside a start |
| `AdmAuthentication`: `Failed to fetch an access token` | 2 in 2 min | A sign-in's `AuthenticateResult` had no `access_token` (one user, `TryCreate` returns null) | Noise unless it grows |

## Verification

You found log lines with timestamps that cover the period you care about. If the period is
missing, the rolling window has already overwritten it. That's the limitation addressed by
[plans/log-persistence-options](../plans/log-persistence-options.md).

## Rollback

Not applicable: this procedure only reads. If you lowered the log level to capture Information
logs, set it back to `warning` (see the `az webapp log config` command in
[logging-and-diagnostics](../observability/logging-and-diagnostics.md)).
