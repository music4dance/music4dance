# Read Production Logs

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01

## When to use

You need to see what the app logged: warnings or errors after an incident, the startup
service-health report after a deploy, antiforgery or rate-limit warnings, and so on. How logging
is configured is described in [logging-and-diagnostics](../observability/logging-and-diagnostics.md).

## Prerequisites / access

- Azure CLI signed in to the subscription, with access to resource group `m4d-Web`; or Kudu
  (SCM) access to the app.
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

## Verification

You found log lines with timestamps that cover the period you care about. If the period is
missing, the rolling window has already overwritten it. That's the limitation addressed by
[plans/log-persistence-options](../plans/log-persistence-options.md).

## Rollback

Not applicable: this procedure only reads. If you lowered the log level to capture Information
logs, set it back to `warning` (see the `az webapp log config` command in
[logging-and-diagnostics](../observability/logging-and-diagnostics.md)).
