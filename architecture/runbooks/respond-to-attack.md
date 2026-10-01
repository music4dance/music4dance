# Respond to an Attack or Traffic Spike on Login/Register

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01

## When to use

You see bursts of `429`s or failed logins, users report being rate-limited or shown CAPTCHAs, or
`/Admin/Diagnostics` shows a spike on `/identity/*`. How the defenses work is described in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md).

## Prerequisites / access

- An admin account (for `/Admin/Diagnostics`).
- Log access, for deeper forensics: Kudu, `/home/LogFiles/Application/`.
- Rights to change App Configuration or app settings, if limits need tuning.

## Steps

1. **Look:** open `/Admin/Diagnostics`.
   - *Rate limiting*: Is it global-limit or per-IP-limit hits? How many unique IPs? Click an IP
     for `RateLimitIpDetail`.
   - *Authentication security*: Are failures spread across many usernames from few IPs
     (credential stuffing)? Or one username from many IPs (account targeting)? Check the
     suspicious-activity breakdown too.
   - *Meta/crawler counts*: A spike here is a link-preview loop, not an attack.
2. **Turn on rate-limit logging** (the toggle on the same page) to get every identity request's
   decision at Information level in the logs. **Turn it off afterwards**, because it's noisy.
3. **Classify:**
   - *Crawler loop*: the user agent matches a known crawler that isn't in the short-circuit list.
     Add the fragment to `KnownCrawlerFragments` in a PR, but never add `whatsapp` or `instagram`.
   - *Distributed attack*: high global count, many IPs. The global limit plus automatic CAPTCHA
     are working as designed. Watch it.
   - *Targeted account*: lockout (3 failures, 15 minutes) protects the account. Consider
     contacting the user.
4. **Tune, if real users are being hurt:** raise `RateLimiting:GlobalMaxRequestsPerWindow` or
   `CaptchaThresholdPercent` in App Configuration (label `Production`). Make sure the `Captcha`
   feature flag is on, so escalation actually challenges instead of only blocking.
5. **Record it:** add a dated entry to the History section of
   [bot-and-abuse-defense](../security/bot-and-abuse-defense.md) with what happened, its scale,
   and any changes made.

## Verification

Limited-request counts drop back to baseline in the hourly table, failed logins stop clustering,
and no new user reports come in.

## Rollback

Revert any App Configuration changes to the `RateLimiting` values. All of the tracker state is
in-memory, so restarting the app clears the counters and a forced CAPTCHA (it would expire within
5 minutes anyway).
