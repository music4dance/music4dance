# Read Production Logs

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-13

## When to use

You need to see what the app logged: warnings or errors after an incident, the startup
service-health report after a deploy, antiforgery or rate-limit warnings, and so on. How logging
is configured is described in [logging-and-diagnostics](../observability/logging-and-diagnostics.md).

## Prerequisites / access

- Azure CLI signed in to the subscription, with access to resource group `m4d-Web`; or Kudu
  (SCM) access to the app. The subscription is in the `music4dance` tenant: use
  `az login --tenant 77fd022e-b4cb-4681-aa1d-c4f8ec6bec90`. A bare `az login` fails with
  AADSTS50076 because both tenants on the account require MFA, and only `--tenant` prompts for
  it.
- The `analyze-logs` Claude Code skill (`.claude/skills/analyze-logs/`) automates steps 2 and 3:
  it downloads the logs, unzips them to `local/logs/msc4dnc-<date>/`, and groups them into
  signatures.
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
   - Download everything:
     ```bash
     az webapp log download --name msc4dnc --resource-group m4d-Web --log-file local/msc4dnc-logs.zip
     ```
     Then unzip under `local/` (it's gitignored) and grep, e.g.
     `grep -rn "Antiforgery validation failed" local/msc4dnc-logs/`.
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

## Known signatures

Check here before investigating. Counts are for the 2026-10-12 to 2026-10-13 download (about 1,440
entries).

| Signature | Count | Cause | Status |
| --- | --- | --- | --- |
| Kestrel `An unhandled exception was thrown` → `BadImageFormatException: Index not found` (from `Logger'1..ctor`), followed by `The response has already started` | 213 + 39 in 4½ min | `AzureWebApp@1` zip-deploys over `wwwroot` while the old process is still serving, so assemblies change underneath it. The burst runs from deploy start until the new container's health report | **Open**: Run From Package or a staging slot swap |
| `Couldn't find dance <id>` from `DanceQueryItem.FromValue` via `BotFilter.cshtml` | 10 | Crawlers send song filters naming dances that don't exist (`4387`, `DanceTags`); parsing threw during view render, giving a 500 | **Fixed 2026-10-13**: `DanceQuery.IsValid` check returns 400 in `SongController.DoAzureSearch` and `CustomSearchController.Index` |
| `Azure.Core` `404 Not Found` with a `Sync-Token` header, every ~5 min | ~815 | App Configuration refresh polls `Configuration:Sentinel` with label `Production`, which doesn't exist (only unlabeled and `Staging` do). Also means bumping a sentinel never refreshes production | **Open**: create the `Production`-labeled sentinel |
| `Azure.Core` `404 Not Found` with `request-id`/`elapsed-time` headers | ~100 | Azure AI Search. Probably `FindSong` lookups for IDs not in the index (unconfirmed) | Probably noise |
| `Azure.Core` `400 Bad Request` with `request-id`/`elapsed-time` headers, in pairs | 70 | Azure AI Search rejects a request. The pairs suggest `SongIndex.DoSearch`'s retry-without-select (unconfirmed). No matching 500s were logged, and the error body only goes to `Trace.WriteLine`, so the query and the caller are unknown | **Open**: log the `RequestFailedException` message |
| `Azure.Core` `Response … took Ns` | 57 | Slow Azure responses (App Configuration, Search) | Watch; see the App Configuration timeout work |
| `LuckyPennySoftware.AutoMapper.License`: license expired | 2 per start | AutoMapper's commercial license key (`Authentication:AutoMapper:Key`) expired around 2026-09-23 | **Open**: renew, or replace AutoMapper |
| `Spotify request …/playlists/<id> failed with 404` | 38 in one 9-min run | A batch reads playlists that are gone. The `37i9dQZF1D…` IDs are Spotify-owned editorial playlists, which the Web API stopped serving to new apps in Nov 2024 | **Open**: mark dead playlists and skip them |
| `Antiforgery validation failed for suggestion request`, once per keystroke | 90 | No cookie (`HasAntiforgeryCookie=False`), a cookie/token mismatch, or a token for a different user after signing in from another tab. Each keystroke logs again | **Open**: see the 4xx triage log; consider re-fetching the token on failure and logging once per page |
| `Antiforgery validation failed for usage log batch` | 6 | Same causes, from the usage beacon | Noise unless it grows |
| `ArtistsCoverage (SongIndexExperimental) failed`: index `songs-experimental` not found | 2 | Admin page run against an index that doesn't exist in production | Noise |
| `HttpsRedirectionMiddleware`: `Failed to determine the https port` | 1 per start | App Service terminates TLS in front of the container | Noise |

## Verification

You found log lines with timestamps that cover the period you care about. If the period is
missing, the rolling window has already overwritten it. That's the limitation addressed by
[plans/log-persistence-options](../plans/log-persistence-options.md).

## Rollback

Not applicable: this procedure only reads. If you lowered the log level to capture Information
logs, set it back to `warning` (see the `az webapp log config` command in
[logging-and-diagnostics](../observability/logging-and-diagnostics.md)).
