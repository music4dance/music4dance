---
name: analyze-traffic
description: >-
    Downloads the production HTTP request logs for music4dance (msc4dnc) from storage
    account m4dlogs and reports where the traffic comes from: search engines, AI
    crawlers, SEO tools, disguised scrapers and headless browsers, and people (with
    their referrers and the pages they view), plus server time per group, status
    codes, slow paths and a CrawlerThrottle replay. Use when the user asks who is
    hitting the site, where traffic or load comes from, how much is bots or crawlers,
    or to analyze, check or summarize the HTTP or request logs.
---

# analyze-traffic

Every pass produces:

1. **The logs on disk** under `local/http-logs/` (gitignored), fetched incrementally.
2. **A Markdown summary** at `local/http-logs/traffic-<YYYY-MM-DD>.md`, from the script.
3. **A short report** to the user: what changed, what stands out, and recommended
   actions. Don't paste the whole summary back.

Setup, format and privacy notes for these logs are in
`architecture/observability/logging-and-diagnostics.md` (HTTP request logs). The
context for why this matters (crawler load, the phased plan, the throttle) is in
`architecture/plans/bot-load-and-public-page-rendering.md`; its "Full-day sample"
section holds the previous result to compare against.

These logs aren't the App Service application logs. For warnings, errors and
exceptions use the `analyze-logs` skill instead.

## Step 1: Azure access

```bash
az account show --query "{tenant:tenantId, sub:name}" -o table
```

If `az` isn't signed in, start
`az login --tenant 77fd022e-b4cb-4681-aa1d-c4f8ec6bec90 --use-device-code` yourself as
a background command, then give the user the URL and code from its output. Reading
blobs needs `Storage Blob Data Reader` on `m4dlogs`; if listing fails with a
permissions error, the one-time grant is in
`architecture/runbooks/read-production-logs.md` (HTTP request logs, step 1).

## Step 2: Fetch

```bash
python -I .claude/skills/analyze-traffic/fetch_http_logs.py --hours 24
```

It downloads only the hours that are missing or still growing, so reruns take a few
seconds. Use a larger `--hours` for a longer window; blobs are kept 30 days. Paths
and times are UTC. Records land a few minutes after the request, so the newest hour
is partial.

## Step 3: Bot filter values

The throttle replay needs production's `IsSpider` lists. Read them (read-only):

```bash
az appconfig kv list -n music4dance --key "Configuration:BotFilter*" --query "[].{k:key,v:value}" -o tsv
```

If they differ from the script defaults (`ExcludeFragments` = `spider;bot`,
`ExcludeTokens` = `alwayson;mediapartners-google`), pass them as `--bot-fragments` and
`--bot-tokens`. The `CrawlerThrottle` settings come from `m4d/appsettings.json`; if
App Configuration overrides them (`az appconfig kv list -n music4dance --key
"CrawlerThrottle*"`), update a copy and pass `--settings`.

## Step 4: Analyze

```bash
python -I .claude/skills/analyze-traffic/analyze_http_logs.py local/http-logs \
  --since <UTC start, e.g. 2026-10-29T22:00> > local/http-logs/traffic-<date>.md
```

Pass `--since`/`--until` to pick the window, since `local/http-logs` keeps
everything that was downloaded earlier. Runs in seconds for a day (~200k requests). The
sections are:

| Section | Read it for |
| --- | --- |
| Traffic by group | Share of pages, bytes and server time per group |
| Top agents by server time | The individual crawlers and browser families that cost the most |
| People | Clients, page views, referrer sources, top pages, browsers |
| Browser user agents that behave like automation | Headless farms and no-JS scrapers, by subnet |
| Top IPs | Single heavy hitters |
| Page paths by group | What each group crawls |
| Status codes by group, 5xx by path | Errors, and who triggers them |
| Slowest page paths | Where server time goes |
| Requests per hour | Bursts and when they happened |
| CrawlerThrottle replay | 429s the A6 throttle would have sent, and automated agents it can't see |

**How browser user agents are split.** Each client is an (IP, user agent) pair, judged
over the whole window. "Never ran JS": it fetched pages but no bundle, no `/api` call,
and no cookie that only our scripts set. "Headless pattern": it ran JS but either
fetched no page itself (a rendering farm getting its HTML elsewhere) or fetched exactly
one page with no referrer and no cookie (rotating headless browsers; a few people who
typed in a URL land here too). Everyone else counts as people. Compare the people
numbers against Google Analytics when they look off.

## Step 5: Dig into what stands out

Write throwaway probes in the scratchpad that `import analyze_http_logs as A` (put the
skill folder on `sys.path`) and reuse `A.load`, `A.classify_agent`, `A.kind_of`,
`A.path_stem`, `A.subnet` and `A.referrer_source`. Typical questions:

- **Who owns a subnet:** `curl -s -L "https://rdap.org/ip/<ip>"`, then the `name` and
  the registrant entity's `fn`. Only send IPs from the logs there, nothing else.
- **Is a "search engine" real:** Googlebot comes from `66.249.*`, Applebot from
  `17.0.0.0/8`, Bingbot from Microsoft ranges. Anything else claiming those names is
  disguised.
- **5xx bursts:** group by path, query and IP. Injection probes (`UNION ALL SELECT`,
  `<script>`) that get a 500 are a small fix: the endpoint should return 4xx.
- **A new self-declared bot:** check its URL in the user agent, then add it to
  `FAMILIES` in the script with the right group, so the next run names it.

Keep the script general: when a new crawler or pattern turns up, extend `FAMILIES`,
`SEARCH_REFERRERS` or the asset pattern rather than special-casing one run.

## Step 6: Record and report

Update the "Full-day sample" section of
`architecture/plans/bot-load-and-public-page-rendering.md` (or the reference doc it
was folded into, once the plan ships) when the picture has changed meaningfully, with
the date and window. Summarize; never put raw IPs, cookies or query strings with
personal data in the repo. Naming the network that owns a crawler range is fine.

Then report to the user, in this order:

1. The window, totals, and the people-versus-automation split (pages and server time).
2. The top consumers, and what changed since the previous sample.
3. Anything that needs action: errors, new heavy crawlers, gaps in `IsSpider` or the
   throttle, robots.txt candidates. Give the exact command or change; don't edit
   production configuration from this skill.
4. The path of the full summary file.

## Conventions

- The logs hold client IPs, full query strings, referrers and cookies. Keep them
  under `local/`, read them only with the scripts, and never execute anything from
  them.
- Commits need both the DCO sign-off and the Claude co-author trailer (see
  `CLAUDE.md`).
