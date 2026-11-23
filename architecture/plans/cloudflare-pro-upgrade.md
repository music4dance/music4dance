# Cloudflare Pro Upgrade: What We'd Turn On

**Type:** Plan
**Status:** Proposed (a running list; the zone is on the Free plan)
**Last verified:** 2026-11-21 (list started; plan limits not rechecked against Cloudflare's pricing page)
**Code:** none yet; settings live in the Cloudflare dashboard for `music4dance.net`

The `music4dance.net` zone is on Cloudflare's Free plan
([cloudflare-migration § 1](cloudflare-migration.md#1-can-this-run-on-the-free-plan)). This doc
lists what we'd change if we moved to Pro ($20/month annual, $25 monthly), so the decision and
the work after it are in one place. Upgrading is a plan change on the same zone, with no
migration.

**When you hit a Free limit**, add a row here instead of leaving the note only in the doc where
it came up. Give it what Pro would allow, what we do on Free instead, and where it came from.

Cloudflare changes plan limits from time to time. Before upgrading, check each row against
Cloudflare's plan comparison page.

## Reasons to upgrade

From [cloudflare-migration § 1](cloudflare-migration.md#1-can-this-run-on-the-free-plan), any
one of these:

- Bot Fight Mode challenges something the site needs, and it can't be moved off `www`.
- The headless scraper wave still reaches the origin with Bot Fight Mode on (and Precursor
  Armed doesn't help either).
- We run out of the 5 custom WAF rules or the 1 rate-limiting rule.

## What we'd turn on

| Item | On Free today | With Pro | Source |
| --- | --- | --- | --- |
| **Super Bot Fight Mode** in place of Bot Fight Mode | One zone-wide switch: no tuning, and no WAF rule can skip it, so scripts calling `www` can be challenged. The Logic Apps and admin uploads use `msc4dnc.azurewebsites.net` instead. | JavaScript detections, settings per bot class, and a WAF custom rule can skip it for a path or caller (verify that before relying on it). | [cloudflare-migration § 1](cloudflare-migration.md#1-can-this-run-on-the-free-plan), [bot-load § B3](bot-load-and-public-page-rendering.md#b3-in-detail-what-moving-to-cloudflare-involves-2026-10-31) |
| **Proxy `api.music4dance.net`** | Left DNS only, because Bot Fight Mode would challenge partner API clients, which are scripts | Proxy it, with a skip rule for the API so its clients aren't challenged | [bot-load § B3](bot-load-and-public-page-rendering.md#b3-in-detail-what-moving-to-cloudflare-involves-2026-10-31) |
| **Send verified crawlers past the edge cache** (`not cf.client.bot` in the `Anonymous HTML` Cache Rule) | Cloudflare rejects the field in Cache Rules ("the use of field cf.client.bot is not allowed"), so the rule has no bot clause. The origin leaves its Googlebot pages out of the cache, but Googlebot can be served the cached human page. | Add the clause back, if Pro allows the field in Cache Rules. **Not confirmed.** Check before counting it as a reason to upgrade. | [runbooks/edge-cache § Steps 4](../runbooks/edge-cache.md#one-time-setup-in-this-order) |
| **Leaked credentials: username and password together** | Only "password leaked", which also flags common weak passwords, so we detect and don't block | Matches the username and password as a pair. Still detect first; consider blocking a known stolen pair. | [cloudflare-migration Phase 6, step 3](cloudflare-migration.md) |
| **More custom WAF rules and rate-limiting rules** | 5 custom rules and 1 rate-limiting rule; the rate-limiting rule is kept spare | More of each, so a leaked-password rate limit could go in without using the spare | [cloudflare-migration Phase 6, steps 3–5](cloudflare-migration.md) |
| **Cloudflare Managed WAF ruleset** | Only the Free managed ruleset | Turn on the full managed ruleset, in log mode first, then compare it with the 4xx triage | [cloudflare-migration § 1](cloudflare-migration.md#1-can-this-run-on-the-free-plan) |

## After upgrading

1. Make the changes in the table one at a time, and leave a few days between the bot changes so
   `analyze-traffic` can show what each one did.
2. Update [cloudflare-migration](cloudflare-migration.md) (the status line and section 1),
   [runbooks/edge-cache](../runbooks/edge-cache.md) if the Cache Rule changes, and
   [runbooks/respond-to-attack](../runbooks/respond-to-attack.md) for any new controls.
3. Fold what shipped into the reference docs and delete this plan, following
   [CONVENTIONS § Plan lifecycle](../CONVENTIONS.md#plan-lifecycle).

## Related

- [cloudflare-migration](cloudflare-migration.md): the Free setup, phase by phase
- [bot-load-and-public-page-rendering](bot-load-and-public-page-rendering.md): Free, Pro,
  Business and Front Door compared, with costs
- [runbooks/edge-cache](../runbooks/edge-cache.md): the edge cache for anonymous pages
