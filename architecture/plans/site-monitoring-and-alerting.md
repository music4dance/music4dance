# Site Monitoring and Alerting

**Type:** Plan
**Status:** Partially implemented: phase 1a (Azure Monitor metric alerts) created 2026-11-20 and
recorded in [service-resilience](../infrastructure/service-resilience.md#site-slow-or-down-azure-monitor-metric-alerts);
the uptime monitor (1b) and phases 2 and 3 not started
**Last verified:** 2026-11-20
**Code:** (none yet) touches `m4d/Middleware/RequestLogFieldsMiddleware.cs`,
`m4d/Services/ServiceHealth/ServiceHealthNotifier.cs`, `m4d/Views/Admin/Diagnostics.cshtml`,
`m4d/Security/` (`AuthenticationTracker`, `RateLimitingTracker`),
`.claude/skills/analyze-logs/fetch_logs.py`, `.claude/skills/analyze-traffic/analyze_http_logs.py`

Today, finding out that the site is struggling depends on someone noticing. Log analysis runs
only when someone asks for the `analyze-traffic` or `analyze-logs` skill. Nothing tells the
admins the site is slow or down. The only proactive emails are `ServiceHealthNotifier`'s
dependency failure and recovery emails
([service-resilience § Admin notifications](../infrastructure/service-resilience.md#admin-notifications))
and the App Configuration quota alert.

This plan adds three things, all by **email only**:

1. **An alert when the site is down or very slow.**
2. **Regular traffic analysis, and an alert when a new traffic incident starts.** The
   residential-proxy scraping waves are the motivating case.
3. **Alerts on attacks against sign-in and registration** (failed logins, credential stuffing,
   lockouts). These were proposed in
   [attack-mitigation-phase2](attack-mitigation-phase2.md) on top of Application Insights. They
   moved here (decided 2026-11-20) and are built into the in-app detector instead (section 4).

Some noise is fine while the thresholds are tuned. Every threshold below is a starting point.

**Decided 2026-11-20:** all alerts go to the **existing admin action group** and the existing
`ServiceHealthNotifier` recipients. The Phase 2 digest runs daily at about 06:00 local time,
with the plain summary sent on Mondays only.

## 1. Why the obvious checks aren't enough

The late-October incident decides what has to be measured. From
[bot-load-and-public-page-rendering § 1.1](bot-load-and-public-page-rendering.md):

- On 2026-10-31 a wave arrived under one user agent (`Chrome/153.0.0.0`, Windows) from
  **54,000 IPs**, at most 58 requests per IP, from consumer ISP ranges. It ran the page's JS
  and sent no cookies. It came in **1–3 minute bursts about once an hour**, peaking at about
  3,200 pages a minute (the median minute was 34).
- During the worst bursts, **page latency for everyone went from a 13 ms median to about
  3.6 s**. Most of the wave's own requests ended as `499` (the client gave up). There was **no
  5xx and no restart**, and it recovered within a minute or two each time.
- The same signature came back on 2026-11-18: about 10,800 IPs in 13 minutes, and two Search
  throttling bursts.

So:

- **The site never went down.** `/health/ready` kept returning 200. A plain up/down check would
  have stayed green, so **response time has to be measured**.
- **The bursts are short.** A check every 5 minutes can land between bursts. Response-time
  alerting needs a window of a few minutes, evaluated every minute.
- **No single IP stands out**, so per-IP counting can't detect a wave. The useful signals are:
  one user agent across thousands of distinct IPs, no cookies, page bursts, and latency jumps.

## 2. Down or slow: alerts outside the app (Phase 1, no code)

Two independent checks. Each covers failures the other can't see.

### 2a. Azure Monitor metric alerts on `msc4dnc`

These reuse the **existing action group** that already emails the admins for the App
Configuration quota alert ([service-resilience](../infrastructure/service-resilience.md)).
Starting thresholds:

| Alert | Metric (aggregation) | Window / frequency | Threshold | Severity |
| --- | --- | --- | --- | --- |
| Site slow | `HttpResponseTime` (Average) | 5 min / 1 min | > 1 s | 2 |
| Site very slow | `HttpResponseTime` (Average) | 5 min / 1 min | > 3 s | 1 |
| Server errors | `Http5xx` (Total) | 5 min / 1 min | > 10 | 2 |
| Health check failing | `HealthCheckStatus` (Average) | 5 min / 1 min | < 100 | 1 |
| Traffic spike | `Requests` (Total) | 5 min / 5 min | > 5,000 | 3 |

- **Cost:** metric alert rules are about $0.10 per monitored time series a month, so this set
  costs about $0.50 a month.
- **Replayed against the 1-minute metrics (2026-11-20)**, using a request-weighted 5-minute
  rolling average:

  | Day | 5-min average response time: median / max | Requests per 5 min: median / max | Windows over 1 s | Windows over 3 s |
  | --- | --- | --- | --- | --- |
  | 2026-10-31 (wave) | 0.021 s / 3.93 s | 598 / 53,728 | 9 | 3 |
  | 2026-11-18 (wave, Search throttling) | 0.030 s / 15.9 s | 228 / 8,101 | 18 | 13 |
  | 2026-11-19 (normal) | 0.024 s / 0.54 s | 265 / 4,190 | 0 | 0 |

  Both incidents would have fired "slow" and "very slow", and the normal day fires neither.
  The spike threshold is 5,000, not the 15,000 first proposed: 15,000 would have missed the
  11-18 wave (8,101), and a normal day reached 4,190. Http5xx stayed at 0–1 per 5 minutes
  on all three days, including both waves, so the 5xx alert catches a different kind of
  failure.
- **Alert state:** Azure sends one email when an alert fires and one when it resolves (with
  auto-resolve on). That gives an incident's start and end without any extra work.
- **Blind spots:** these metrics come from the App Service front ends. They can't see DNS,
  Cloudflare, or a failure in Azure's alerting path. Section 2b covers those.
- **Created 2026-11-20** with `az monitor metrics alert create`, one per row, scoped to `msc4dnc`
  and sent to the `m4d-admins` action group, with auto-resolve on. The as-built rules are
  in [service-resilience](../infrastructure/service-resilience.md#site-slow-or-down-azure-monitor-metric-alerts).

### 2b. External uptime monitor

Use a free hosted uptime service (UptimeRobot, Better Stack, or similar), with email alerts
only:

- **URL:** `https://www.music4dance.net/health/ready`. It goes through Cloudflare (so DNS, TLS
  and the edge are checked) and isn't cached, so the response comes from the origin. It answers
  `200 ready` only when the database is healthy. **Don't check a content page**: once the edge
  cache is on ([runbooks/edge-cache](../runbooks/edge-cache.md)), a page would be served from
  Cloudflare's copy and say nothing about the origin.
- **Check:** HTTP status 200, with the keyword `ready` if the service supports it. Use the
  shortest interval the free tier allows (about 3–5 minutes). Set the timeout as short as
  allowed, ideally around 10 s. A timeout counts as down, which is the nearest a free tier
  gets to detecting "very slow".
- **Second monitor (optional):** `https://music4dance.net/health/ready` (the apex), so a
  broken apex redirect or DNS record is also caught.
- **Response-time history:** most of these services keep a response-time chart even on the
  free tier. That's useful afterwards, when an alert fires.
- The owner signs up and creates the monitors. The account and alert address are personal,
  so this isn't automated from the repo.

## 3. Daily traffic digest on the Mac mini (Phase 2)

Run the existing analysis every day, without anyone asking, on the owner's Mac mini, which is
on all the time. The development machine isn't. It sends an email digest, with a clear flag
when something new or abnormal shows up.

**Why this comes before the in-app detector:** it's mostly existing code, and each day's
numbers become the baseline for choosing the detector's thresholds in Phase 3.

### What runs

A `launchd` job (a `~/Library/LaunchAgents` plist; not `cron`, which macOS throttles) runs
once a day at about 06:00 local time:

1. `git pull` in a clone of the repo on the Mac mini, so the bot classification and throttle
   settings in `m4d/appsettings.json` stay current.
2. `fetch_logs.py requests --hours 26` and `fetch_logs.py http --hours 26`. Both read blob
   storage `m4dlogs`. They're incremental and reuse what's already in `local/logs/`.
3. `analyze_http_logs.py` for the previous UTC day. It writes the usual
   `local/logs/reports/traffic-<date>.md`, plus a new **JSON summary** (see below).
4. A new `traffic_digest.py` compares the day's summary with the previous 14 days and sends
   one email.

The application logs (`analyze-logs`) stay manual. Downloading them needs App Service rights,
not just blob reads, and the daily job should get the least access it can.

### Changes to the scripts

- **`analyze_http_logs.py --json <path>`**: writes the numbers the digest compares. Per day:
  requests, pages, and server time per group; status counts (`429`, `499`, 5xx); page p50 and
  p95; the top user agents by server time, with each one's distinct-IP count and cookie-less
  share; the people page-view count. The Markdown report doesn't change.
- **`traffic_digest.py`** (new, beside the other script in `.claude/skills/analyze-traffic/`)
  flags:
  - a user agent in the day's top 20 by server time that wasn't in any of the previous 14
    days;
  - any user agent seen from more than 1,000 distinct IPs (the residential-proxy signature);
  - any group's pages or server time above 2x its 14-day median;
  - page p95 above 1 s, or any minute with page p95 above 3 s;
  - `499` or 5xx counts above 3x their median;
  - people page views below half their median (a sign that people are being blocked or
    throttled).
- **The email:** the subject starts with `[m4d traffic] INCIDENT` when any flag fires, else
  `[m4d traffic] daily`. The body lists the flags first, then a short table (totals, groups,
  top user agents) and the path to the full report on the Mac mini. **No IPs or query strings
  go in the email**, only counts and user agents. The logs and full reports stay on the Mac
  mini under `local/`, as the logging doc requires.
- **Volume:** to keep noise down once things settle, send the plain daily email only on
  Mondays (a weekly summary), and send `INCIDENT` emails every day.

### Access and secrets on the Mac mini

- **Azure:** a dedicated service principal with **Storage Blob Data Reader on `m4dlogs` only**,
  signed in with a certificate (`az login --service-principal --certificate ...`). A personal
  `az login` on an unattended machine would lapse after about 90 days of non-interactive use,
  and would carry far more access than the job needs. `fetch_logs.py` calls `az` with
  `--auth-mode login`, so it works unchanged once the service principal is signed in.
- **Email:** send through the existing Azure Communication Services resource, with
  `donotreply@music4dance.net` as the sender, using the `azure-communication-email` Python
  package. The connection string is kept in the macOS Keychain and read with
  `security find-generic-password`, never in the repo or the plist. When
  [acs-email-migration](acs-email-migration.md) moves the app to an SMTP provider, the digest
  should move with it (Python's `smtplib`).
- **Failure:** if any step fails (expired credential, `az` error, missing day of logs), the job
  sends `[m4d traffic] job failed` with the error text, so it can't go quiet without anyone
  knowing. A missing digest is the backstop: no email by 07:00 means the Mac mini or the job is
  down.

## 4. In-app detector: traffic waves and attacks (Phase 3, code)

The digest reports the morning after. The detector reports **while a wave or an attack is
happening**, within minutes, using signals that Azure's metrics don't have. It has two signal
sets: traffic waves (below) and security (the next subsection). Both share the evaluation loop,
the cooldowns, the emails and the admin panel.

### Design

- **Collection:** a small middleware just after `RequestLogFieldsMiddleware` (so the visitor IP
  from `ClientAddresses` and the cookie names are already known) records each request into a
  singleton `TrafficWatch`: minute, user agent, client IP, whether it's a page, whether it sent
  any cookies, status and elapsed time. Static files are counted but not tracked per IP.
- **Bounded memory:** rolling 1-minute buckets kept for 15 minutes. Distinct IPs are counted
  per user agent, only for the top 50 user agents in the window, with a cap per user agent
  (for example 20,000) so a wave can't grow memory without limit. A cheap cardinality
  estimate is fine. Exact counts aren't needed.
- **Evaluation:** a `BackgroundService` checks the window once a minute against these
  signals (starting thresholds, all configurable):

  | Signal | Starting threshold | Matches |
  | --- | --- | --- |
  | Distributed agent | One user agent from more than 500 distinct IPs in 10 minutes, over 80% of its requests without cookies | The residential-proxy wave (about 10,800 IPs in 13 minutes on 2026-11-18) |
  | Page burst | More than 600 anonymous page requests in a minute | The wave's peaks (about 3,200 a minute; a normal minute is about 35) |
  | Latency | Page p95 above 2 s for 2 consecutive minutes | The 2026-10-31 slowdowns |
  | Client give-ups | More than 200 `499` responses in 5 minutes | The wave's requests timing out |

- **Email:** through `ServiceHealthNotifier.SendAsync` and its recipients, under its own
  config section. One `Traffic incident started` email names the signal(s), the user agent,
  its request, IP and page counts, the top paths (without query strings), and the page latency.
  The cooldown is per signal, defaulting to 60 minutes, so an hourly wave means one email an
  hour at most. A `Traffic incident ended` email follows after 30 quiet minutes, with how long
  it lasted and the totals. Bursts that come back within the quiet period belong to the same
  incident.
- **Admin page:** a "Traffic watch" panel on `/Admin/Diagnostics` shows the current window (top
  user agents with distinct IPs, pages a minute, p95) and the last 10 incidents, kept in memory
  until restart.
- **Config:** a `TrafficWatch` section (`Enabled`, the thresholds, `CooldownMinutes`,
  `QuietMinutesToEnd`), in App Configuration so thresholds can be tuned without a deploy (bump
  `Configuration:Sentinel`). Read the values on each evaluation, not once at startup.
- **Multiple instances:** each instance sees only its own requests. At one instance that's
  everything. If the plan scales out, a wave split across instances may fall below the
  thresholds. Note it, and lower the thresholds then if needed.
- **Watch only:** the detector doesn't block or throttle anything. Acting on a wave (for
  example, telling Cloudflare to challenge that user agent) is a separate decision. See
  [cloudflare-migration](cloudflare-migration.md) and
  [bot-and-abuse-defense](../security/bot-and-abuse-defense.md).

### Security signals (from attack-mitigation-phase2)

The identity defenses already record what these alerts need, in memory and per instance
([bot-and-abuse-defense](../security/bot-and-abuse-defense.md)):

- `AuthenticationTracker`: the last 1,000 login and registration attempts (or 24 hours), with
  the outcome (`InvalidPassword`, `UserNotFound`, `LockedOut`, or an Identity error code).
- `RateLimitingTracker`: the last 10,000 rate-limit decisions on `/identity/*`, IPs anonymized.
- `LoginModelBase`'s suspicious non-local `returnUrl` records.

So the security signals need **no new telemetry and no Application Insights**. The evaluation
loop reads these trackers each minute, alongside `TrafficWatch`. Starting thresholds, carried
over from attack-mitigation-phase2 (its "warning" level; the "critical" level only adds a
second email, so it's dropped while thresholds are tuned):

| Signal | Starting threshold | Matches |
| --- | --- | --- |
| Failed logins (total) | More than 50 in an hour | Brute force across many accounts |
| Failed logins (one username) | More than 5 in 5 minutes | Brute force on one account |
| Distinct IPs per username | More than 5 in 5 minutes | Distributed credential stuffing |
| Account lockouts | More than 10 in an hour | Many accounts under attack |
| Identity rate-limit hits (global limit) | More than 10 in an hour | Login flooding |
| Suspicious `returnUrl` | More than 5 in an hour | Open-redirect probing |

- **The email** names the signal, the window, the counts, the targeted usernames (masked
  after the first two characters), and the top anonymized IPs, as the trackers hold them.
  It includes `Security incident` in the subject so it can be filtered apart from traffic
  incidents.
- **Cooldown:** 15 minutes per signal (attack-mitigation-phase2's figure), shorter than the
  traffic signals' 60, because an attack on accounts is more urgent than a scraper.
- **Buffer limits:** `AuthenticationTracker` keeps 1,000 attempts. Under a large attack the
  buffer turns over in minutes. That still trips the thresholds, but the hourly counts in the
  email are a lower bound; say so in the email when the buffer is full.
- **Not carried over:** the Application Insights setup, the "hourly digest during active
  attacks" (the end email and the admin panel cover it), and the anomaly-detection and
  machine-learning ideas (attack-mitigation-phase2 Phase 3), which needed Application
  Insights. Honeypot, IP-reputation and user-agent alerts belong with those features, which
  stay in attack-mitigation-phase2; when they're built, they add signals here.

### Tests

- Unit tests for `TrafficWatch` that feed synthetic request streams: a distributed-agent wave
  fires once and ends once; a single heavy IP doesn't fire "distributed agent"; Googlebot at
  normal volume doesn't fire; memory stays within the caps under a 50,000-IP stream.
- Security signals: synthetic `AuthenticationTracker` and `RateLimitingTracker` contents that
  cross each threshold fire once per cooldown; ordinary failed logins (a few an hour) don't;
  usernames are masked in the email.
- A notifier test with the existing `FileEmailSender` / `NullEmailSender` that checks the
  cooldown and the end email.

## 5. Options considered and not chosen

| Option | Why not |
| --- | --- |
| Application Insights availability tests | Billed per run, and they need App Insights back, which caused the earlier billing surprise ([log-persistence-options](log-persistence-options.md)). The external monitor in 2b does the same job for free. |
| Log Analytics queries and log alerts | Rejected for cost in [logging-and-diagnostics](../observability/logging-and-diagnostics.md). |
| Azure Function on a timer over `m4dlogs` | Phase 2 moved to the cloud: more infrastructure to own, for no gain while the Mac mini is available. Reconsider if the Mac mini stops being reliable. |
| Scheduled Claude Code cloud routine running `analyze-traffic` | Would send visitor IPs and query strings into a cloud session, need a stored credential, and cost tokens daily. Run the skill on demand when an alert fires instead. |
| Cloudflare health checks | Need the Pro plan. Turn on whatever notifications the Free plan does offer (for example, DDoS attack alerts) as a no-cost extra. |
| SMS or push notifications | Not wanted for now. Azure action groups and the uptime services can add them later without changing anything else. |

## 6. Phases

1. **Phase 1: alerts outside the app.** Create the 2a metric alerts on the existing action
   group. The owner sets up the 2b uptime monitor. Turn on the Cloudflare Free plan
   notifications. Check the `HttpResponseTime` threshold against the 2026-10-31 burst. No code.
2. **Phase 2: daily digest on the Mac mini.** Add `--json` and `traffic_digest.py`, create the
   service principal and Keychain entry, install the `launchd` job, and write a runbook
   (`runbooks/traffic-digest-mac-mini.md`) for setting it up and renewing the credential.
3. **Phase 3: in-app detector.** Add `TrafficWatch`, the security signals, the notifier emails
   and the admin panel, with traffic thresholds taken from two or more weeks of Phase 2 data.
   Ship it with `Enabled=false`, turn it on, and tune. The security signals can ship first if
   an attack on accounts makes them more urgent: they don't depend on Phase 2 data.

When each phase ships, move it into the reference docs: the alerts and the traffic detector
into [service-resilience](../infrastructure/service-resilience.md), the security signals into
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md), and the digest into
[logging-and-diagnostics](../observability/logging-and-diagnostics.md).

## Related

- [bot-load-and-public-page-rendering](bot-load-and-public-page-rendering.md): the traffic
  measurements and the residential-proxy waves
- [logging-and-diagnostics](../observability/logging-and-diagnostics.md): the request logs the
  digest reads
- [service-resilience](../infrastructure/service-resilience.md): health endpoints, the existing
  admin emails and action group
- [runbooks/read-production-logs](../runbooks/read-production-logs.md) and
  [runbooks/respond-to-attack](../runbooks/respond-to-attack.md)
- [attack-mitigation-phase2](attack-mitigation-phase2.md): the remaining edge and form
  defenses; its monitoring and alerting proposal moved here
- [bot-and-abuse-defense](../security/bot-and-abuse-defense.md): the identity defenses and
  trackers the security signals read
