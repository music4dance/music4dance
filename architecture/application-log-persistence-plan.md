# Application Log Persistence — Options Plan

## Overview

Investigated 2026-09-04: production "warning" logs appeared to be missing. Root cause turned out to be that **nothing persists application logs anywhere today** — `az webapp log tail` / Azure Portal "Log stream" is a live, unbuffered tap on container stdout. If no one is actively connected at the moment a log line is written, it's gone. This affects both `msc4dnc` (production) and `m4d-test` equally; there was never a prod/test config difference.

This document lays out options to make Warning+ level logs durably reviewable after the fact, ordered lightest/cheapest first, so a choice can be made deliberately instead of re-enabling the thing that caused a billing surprise last time.

## Current State (verified via `az` CLI, 2026-09-04)

- **App Service logging** (`az webapp log show`): `applicationLogs.fileSystem.level` and `azureBlobStorage.level` are both `Off` on **both** `msc4dnc` and `m4d-test`. No persistence configured anywhere.
- **Diagnostic settings** on either App Service resource: none (`az monitor diagnostic-settings list` returns `[]`). Nothing is routed to Log Analytics.
- **Application Insights**: an orphaned component named `m4d-staging` still exists (`Microsoft.Insights/components`, kind `web`), but lives in the auto-created `DefaultResourceGroup-WUS` resource group, not `m4d-Web`. It's workspace-based, backed by `DefaultWorkspace-<subscription>-WUS`, a `PerGB2018` (pay-as-you-go) Log Analytics workspace with **30-day retention and no daily cap (`dailyQuotaGb: -1`, unlimited)**. Neither app currently has an `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting, so this component isn't receiving data right now — but the missing daily cap is almost certainly why cost scaled directly with traffic when it *was* wired in. That's the mistake to not repeat.
- The app already calls `logging.AddAzureWebAppDiagnostics()` in [M4dApplicationExtensions.cs:85](../m4d/Configuration/M4dApplicationExtensions.cs#L85) — Options 1 and 2 below need **zero code changes**, only Azure resource configuration, because that provider already reads the Filesystem/Blob toggle at runtime.
- Per [[project_single_instance_deployment]]: production is a single instance, cost-driven, no SLA — favor options that don't add ongoing per-instance or scale-out complexity.

## Goals

1. Warning+ (ideally Information+) logs survive past the moment they're written — reviewable tomorrow, not just live.
2. Cost stays near-zero or is hard-capped — no repeat of the App Insights surprise.
3. Prefer reusing/cleaning up the orphaned `m4d-staging` resources over leaving them stranded or provisioning a third logging resource.

---

## Option 1 — App Service "Application Logging (Filesystem)" (Free) — [IMPLEMENTED 2026-09-04]

Toggle on in Portal (App Service → App Service logs) or via CLI, level `Warning`, for `msc4dnc` (and `m4d-test` if desired):

```
az webapp log config --name msc4dnc --resource-group m4d-Web \
  --application-logging filesystem --level warning
```

Logs land in `/home/LogFiles/Application/*.txt` on the instance, viewable via Kudu (`https://msc4dnc.scm.azurewebsites.net/api/vfs/LogFiles/Application/`) or `az webapp log download`.

- **Cost**: $0 — uses local disk already included with the App Service Plan.
- **Effort**: one toggle, no code change, no new resource.
- **Pros**: immediate, zero risk of surprise billing, works today.
- **Cons**: rolling/quota-bound (old entries get overwritten, no long-term retention), not queryable or alertable, must download/grep manually, lives on the instance disk (fine here since production is single-instance, but wouldn't survive a host move/redeploy that wipes the filesystem).

**Implemented 2026-09-04** on both `msc4dnc` and `m4d-test` via:

```
az webapp log config --name <app> --resource-group m4d-Web \
  --application-logging filesystem --level warning \
  --docker-container-logging filesystem
```

Note: on this Linux/container app, the Portal's "Application Logging" toggle only exposes an on/off "Gather STDOUT and STDERR output from the container" control with no Level dropdown (`--docker-container-logging`, filesystem/off — separate from the classic `--application-logging`/`--level` pair Windows apps expose). Set both flags together; `applicationLogs.fileSystem.level` came back `Warning` on verification for both apps. Side effect observed: this CLI command does a full overwrite of the site's logs config rather than a partial patch, so the unrelated Web server (HTTP) logging retention also changed on both apps (`retentionInDays: 30→3`, `retentionInMb: 35→100` on prod; similar on test) — left as-is, user confirmed it's fine.

## Option 2 — App Service "Application Logging (Blob Storage)" (Pennies/month)

Same toggle, pointed at a Blob container instead of local disk. Needs a Storage Account (reuse an existing one if the project has one for other purposes, or create a small `Standard_LRS` one) and a container SAS URL:

```
az webapp log config --name msc4dnc --resource-group m4d-Web \
  --application-logging azureblobstorage \
  --level warning \
  --blob-container-sas-url "<container-sas-url>" \
  --retention-in-days 30
```

- **Cost**: roughly $0.02/GB-month at Cool tier plus negligible transaction fees — for warning-level app logs at current traffic, likely under $1/month.
- **Effort**: create a Storage Account + container + SAS (or reuse one), one CLI/portal call to point the App Service at it.
- **Pros**: durable — survives restarts/redeploys, retention you control, cheap enough to not think about.
- **Cons**: still plain-text files, not queryable with a query language, no built-in alerting; you'd script/download to search across days.

## Option 3 — Diagnostic Settings → Log Analytics, Basic Logs + hard daily cap (reuses existing workspace, low $/month)

Route just the `AppServiceConsoleLogs` category (skip `AppServiceHTTPLogs` — that's high-volume and not what you're after) to the existing `DefaultWorkspace-...-WUS` Log Analytics workspace via a Diagnostic Setting on `msc4dnc`. Two sub-choices:

- **Basic Logs table plan**: cheaper per-GB ingestion than the default Analytics plan, but 8-day minimum retention and reduced query features (no joins, limited KQL). Good fit for "warnings I want to grep/query for a week or two."
- **Analytics table plan**: full KQL, longer retention, but priced notably higher per GB.

Either way, **set an explicit daily cap this time** (e.g. 50 MB/day) — that's the guardrail that was missing before:

```
az monitor log-analytics workspace update \
  --resource-group DefaultResourceGroup-WUS \
  --workspace-name DefaultWorkspace-35a37095-adba-4229-a691-e55bf38ecf36-WUS \
  --daily-quota-gb 0.05
```

- **Cost**: at Warning-level-only volume this should be low single-digit dollars/month at most; the daily cap makes the worst case bounded and known in advance instead of open-ended.
- **Effort**: moderate — create the diagnostic setting, decide table plan, set the cap.
- **Pros**: real query language (KQL), can build alert rules ("page me if >5 warnings/hour"), reuses the already-provisioned workspace instead of creating a fourth resource.
- **Cons**: most setup of the non-APM options; still Azure Monitor billing, so needs the cap to stay safe.

## Option 4 — Re-enable Application Insights, tuned for cost

Same underlying billing surface as Option 3 (App Insights workspace-based mode bills through the same Log Analytics ingestion), but gets you the full APM experience back: exceptions with stack traces, optional dependency/request timing, Live Metrics, dashboards.

To avoid repeating the surprise:
- Set `ApplicationInsightsLoggerOptions.LogLevel = LogLevel.Warning` so only Warning+ `ILogger` calls are sent — don't let it auto-collect every request/dependency at full volume.
- Turn off adaptive sampling's default "collect everything below threshold" behavior, or set a low fixed-rate sampling percentage if you do want request telemetry.
- Disable Live Metrics (QuickPulse) if unused — it has its own small continuous cost.
- **Set the Daily Cap on the App Insights resource** (Portal: Usage and estimated costs → Daily cap, e.g. $1–2/day) — this is the single control that was missing and is non-negotiable if this option is chosen.

- **Cost**: bounded by whatever daily cap you choose; without tuning, this is what caused the original bill.
- **Effort**: highest of the four — re-add SDK wiring/connection string, configure sampling, set the cap, verify in Portal it's actually applied.
- **Pros**: richest tooling (traces + exceptions + optional perf data + dashboards + alerts) if you want more than logs later.
- **Cons**: most moving parts to misconfigure again; even with a cap, once the cap is hit that day's data is silently dropped, not just billed — a monitoring gap, not just a cost one.

## Option 5 — Third-party free-tier log drain (mention only)

Ship via OpenTelemetry to a SaaS with a generous free tier (Grafana Cloud, Axiom, Better Stack, etc.). Could end up cheapest in dollars, but adds a new external vendor/dependency and OTel wiring not currently in the codebase. Worth a look only if you want dashboards/alerting outside Azure entirely — not recommended as a first step given Options 1–3 already solve "can I see warnings after the fact" using infrastructure you already have.

---

## Recommendation

Given "lightest/cheapest first" and that the immediate problem is just **"I can't see yesterday's warnings"**:

1. **Do Option 1 now** — five-minute free toggle, solves the immediate problem for a rolling window.
2. **Layer Option 2 shortly after** — durable, still effectively free, no code changes, removes the rolling-quota limitation of Option 1.
3. **Only reach for Option 3 or 4** if you find yourself wanting to *query* across days or get alerted proactively rather than checking manually. If so, prefer Option 3 (Basic Logs + explicit daily cap) over re-enabling full App Insights — same cost-safety story, less to misconfigure.

## Orphaned resource cleanup

The `m4d-staging` Application Insights component and its `DefaultWorkspace-...-WUS` Log Analytics workspace still exist in `DefaultResourceGroup-WUS`, unused by either app today. Decide whether to:
- delete both (nothing currently depends on them), or
- keep the workspace and reuse it for Option 3 (in which case set the daily cap regardless of which option is chosen, since it costs nothing to have the guardrail in place), or
- keep as-is and just remember it exists before assuming "no App Insights configured" next time this comes up.

## Open decisions

- [x] Which option(s) to implement, and for which app(s) — Option 1, both `msc4dnc` and `m4d-test`, implemented 2026-09-04
- [ ] Retention/quota values for the chosen option — currently Azure's default filesystem quota for Application Logging; revisit if it fills up faster than expected
- [ ] Whether Option 2 (Blob) is still wanted for longer-than-rolling-window retention
- [ ] Fate of the orphaned `m4d-staging` App Insights + Log Analytics workspace
