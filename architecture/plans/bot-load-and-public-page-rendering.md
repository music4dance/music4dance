# Bot Load and Public-Page Rendering Options

**Type:** Plan
**Status:** Partially implemented: Phase 1 (A1–A7) deployed 2026-10-30 (PR #365) and measured afterwards; Phase 0 partly done (weekday baseline); Phases 2 and 3 not started
**Last verified:** 2026-10-31
**Code:** `m4d/Views/Shared/Vue3.cshtml`, `m4d/Views/Shared/_head.cshtml`, `m4d/Views/Shared/_vue-Layout.cshtml`,
`m4d/Configuration/M4dApplicationExtensions.cs` (cache-control middleware), `m4d/Utilities/SpiderManager.cs`,
`m4d/Controllers/SongController.cs`, `m4d/ClientApp/vite.config.ts`

This plan covers two problems that share a cause. Every public page is an empty `<div id="app">`
plus a large inline JSON model, and the browser has to build the page:

1. **Bot load.** Crawlers (search engines, AI crawlers, SEO tools, scrapers) make most of the
   page requests, and each one gets the full client-rendered payload.
2. **Search ranking.** Google indexes the site, but client rendering costs a lot of page-load
   performance (LCP), and many non-Google crawlers never see the content as HTML.

It records what was measured before and after the first fixes, sets out the options broadly (including ones this
plan doesn't recommend, so they don't need re-researching), compares them, and proposes a
phased path. Signed-in users stay on today's live client rendering in every option. Only the
anonymous experience changes.

## 1. What we measured (2026-10-29 to 2026-10-31)

### 1.1 Traffic mix

Source: the App Service HTTP logs in storage `m4dlogs`
([runbooks/read-production-logs](../runbooks/read-production-logs.md)), analyzed with the
`analyze-traffic` skill (`.claude/skills/analyze-traffic/`). Rerun it to refresh these numbers.

#### Baseline: a full day before Phase 1 (2026-10-29/30)

24 hours, 2026-10-29 21:50 to 2026-10-30 21:48 UTC: 199,385 requests, 51,420 of them pages,
7.7 GB out and 54 minutes of summed server time. Bots are classified by user agent. Browser
user agents are split by behavior: clients that never ran the page's JS, clients with a
headless pattern (ran JS but fetched no page themselves, or one page with no referrer and no
cookie), and everyone else, counted as people.

| Group | Pages | Bytes out | Server time | Biggest members |
| --- | ---: | ---: | ---: | --- |
| SEO tools | 28.0% | 6.0% | 3.8% | DotBot: 14,215 pages from one IP, 5,260 of them `/song/sort` |
| Search engines | 27.7% | 26.8% | 22.8% | Applebot 5,229 pages (genuine, 3,146 Apple IPs), PetalBot 5,789, Bingbot 2,058, Googlebot 840 |
| AI crawlers | 25.4% | 35.3% | 32.0% | Reflectionbot 7,726 pages **plus 59,724 asset requests** (it renders, from 97 ReflectionAI IPs; 22% of server time alone), Exa 3,974, ElevenLabs 746, Bytespider 231 |
| Browser UA, never ran JS | 7.5% | 9.7% | 14.7% | Spread over 1,591 IPs; includes 551 SQL-injection probes that got a 500 |
| Browser UA, headless pattern | 2.5% | 8.3% | 13.6% | 254 IPs in one Ace Data Centers /24 presenting as Linux Chrome 145 (42k requests), rotating Chrome 148–150 from Shanghai and Huawei Cloud ranges |
| Scripts | 3.3% | 5.8% | 3.3% | `DmxMusic/1.0` (1,575 `/song/filtersearch` pages, from a consumer VPN) |
| **People** | **2.4%** | **5.4%** | **8.9%** | 577 clients, 1,230 page views; 160 arrived from Google, 7 from other search engines |

- **Automation is about 97% of page requests and 91% of server time.** A first look at a
  22-minute window on 2026-10-29 (3,210 requests) showed the same split.
- **The expensive targets are album, details and artist pages**, and AI and answer-engine
  crawlers (Exa, ElevenLabs, Reflection, Perplexity, DuckAssist) hit them heavily. Album pages
  averaged about 270 KB each, up to 1.5 MB. **`/song/album` was 37% of server time** (p50
  27 ms, p95 147 ms, max 49.9 s).
- **Few bots run the JS.** Reflectionbot, DuckAssistBot and Applebot load the bundles. Most
  others take only the HTML, so for them the content is just the JSON inside
  `var model_ = ...`.
- **Slow outliers:** bingbot on `/song/album?title=÷ (Deluxe)` took 49.9 s and timed out, and
  `/song/Advanced?...&page=1584` took 10.3 s. A7 fixed the first; deep paging is discussed
  under Phase 1.
- **Absolute load was modest.** Average concurrency was well under one request; bots are most
  of the traffic, but they weren't overwhelming a core. The October out-of-memory restarts
  correlated with a platform base image, not with traffic. The memory angle is in section 1.3.
- **The A6 throttle, replayed over the day,** would have answered 6,440 of 32,804 counted requests
  with 429: DotBot 5,766, Exa 629, ElevenLabs 44. It wouldn't touch Reflectionbot, the largest
  consumer, which averages about 5 pages a minute under one user agent. Agents that `IsSpider`
  doesn't recognize, and so aren't throttled: `DmxMusic/1.0`, the headless Chrome farms,
  Barkrowler (166) and Meta's AI crawlers (`meta-externalagent`, `meta-webindexer`; 38).
  **Done:** AI crawlers stay allowed but throttled. `CrawlerThrottle:AgentLimits` gives
  Reflectionbot 3 a minute (a cap of about 4,300 pages a day, down from 7,700) and throttles
  Barkrowler and the Meta crawlers at the default without sending them the bot stub. With it,
  the replay sends 11,166 of 33,095 counted requests a 429, 4,659 of them to Reflectionbot.
- **Done:** robots.txt now disallows `/searches` (580 requests, 494 from SEO tools; it needs
  sign-in, so they only got a redirect to the login page).
- **Done (2026-10-31):** robots.txt gives `Reflectionbot` its own group that also disallows
  `/song/album` and `/song/artist`. It's a training crawler for Reflection AI's models
  (reflection.ai/bot), so unlike AI assistants and AI search it's unlikely to send visitors,
  and album pages were about 45% of its page requests. It honors robots.txt (Reflection
  says changes apply within about a day) and publishes its IP ranges, so it can be verified.
- **Done:** the SQL-injection probes (`' UNION ALL SELECT …` on `/customsearch?name=` and
  `/song/playlist?id=`, 6 IPs) showed no sign of succeeding, but each one threw a 500. Unknown
  custom-search names and non-base62 playlist ids now get a 404.

#### After Phase 1 deployed (2026-10-31)

22 hours, 2026-10-31 00:00 to 22:00 UTC, starting about an hour after the deploy. Per-hour
figures below leave out the Chrome/153 wave described next, so they compare like with like.

| Per hour | Before (10-29/30) | After (10-31) |
| --- | ---: | ---: |
| Page MB out | 254 | 90 (−65%) |
| `/song/album` average response | 170 KB | 15 KB |
| `/song/artist` average response | 166 KB | 20 KB |
| `/song/details/*` average response | 184 KB | 63 KB (compression only; A2 doesn't trim details) |
| `/song/sort` requests | 219 | 29 |
| `/searches` requests | 23 | 3 |
| Reflectionbot pages / asset requests | 309 / 2,389 | 220 / 892 |
| DotBot pages | 569 | 271 |
| 500s (whole window) | 553 | 0 |
| 429s (whole window) | 0 | 2,321 (Reflectionbot 1,619, DotBot 394, Exa 298, ElevenLabs 13) |
| Server time outside the bursts below | 2.2 min | 2.5–3 min |

- **Phase 1 is doing what it was meant to.** Compression and A2 cut page bytes by two thirds,
  the throttle is answering Reflectionbot, DotBot and Exa with 429s and both are fetching less,
  robots.txt took most crawlers off `/song/sort` and `/searches`, and the injection probes no
  longer cause 500s. Search engines are unaffected (no 429s; Applebot, Bingbot and PetalBot at
  the same rates).
- **A residential-proxy scraping wave started at 16:20 UTC.** 212,000 requests (14,000 page
  requests, 8,177 distinct `/song/details/*` pages, the rest the page's own bundles and
  `/api/health/status`) under one user agent, `Chrome/153.0.0.0` on Windows, from **54,000 IPs**
  (at most 58 per IP), mostly consumer ISP ranges in Latin America and Asia. It runs the page's
  JS, sends no cookies at all, and never appeared before 16:20. It arrives in bursts of 1–3
  minutes roughly every hour, peaking at about 3,200 pages a minute (the median minute is 34).
  In the 17:12–17:13 and 19:16 bursts the server fell behind: page latency for everyone went
  from a 13 ms median to about 3.6 s, and most of the wave's own requests ended as 499 (client
  gave up). It recovered within a minute or two each time, with no 5xx and no restart. The
  wave accounted for about 90% of the day's 19 hours of server time. Nothing in Phase 1 can
  see it: no single IP or user agent stands out enough to throttle without hitting people, and
  a server cache wouldn't help much because most of its pages are distinct.
- **5Six7Eight** (`compatible; 5Six7Eight/1.0; Anzeige fuer Tanzvereine`, one German IP) made
  10,000 `/song/search` lookups, about 7 a minute all day. It's polite and cheap (27 server
  minutes), but `IsSpider` doesn't recognize it, so the throttle ignores it. `DmxMusic/1.0`
  continues (2,637 `/song/filtersearch` pages).
- **Output cache estimate (B1).** Replaying the day's anonymous page requests through a cache
  keyed on path and query: 18% would be hits at a 5-minute TTL, 22% at 6 hours. About 5,100 of
  those hits are the wave retrying the same details pages during a burst, so for everyone else
  the hit rate is nearer 13%.

#### After the Cloudflare DNS move (2026-11-05/06)

22 hours, 2026-11-05 21:00 to 2026-11-06 19:00 UTC, starting at the
[nameserver switch](cloudflare-migration.md) (Phase 3, nothing proxied yet). 188,000 requests,
37,500 pages, **0.5 hours of server time** (about 1–2 minutes an hour, the same as on 11-04).

- **The DNS move changed nothing visible.** No requests came through Cloudflare (6 of 188,000
  from Cloudflare addresses, WARP clients). Host shares, the default host's trickle (54 anonymous
  requests, mostly `/` and probes) and the AlwaysOn 499 rate (about 1 an hour) match the 11-04
  baseline.
- **The Chrome/153 wave is quiet.** The headless group is now mostly one `/24` that also sends
  `ExaSearchBot`, so it is Exa's renderer, running JS on `/song/album`, `/song/artist` and
  details pages at about 1,500 requests an hour. It was in the 11-04 sample too.
- **ShapBot** (Parallel Web Systems, `parallel.ai`, a web search API for AI agents; Google
  Cloud addresses) went from about 400 requests a day to about 17,000 an hour from before
  12:00 UTC on 11-05 until 01:00 on 11-06, then stopped. About 1,350 pages an hour; the rest
  were the page's bundles, icons and `/api/health/status`, because it renders. `IsSpider`
  recognizes it, so the throttle answered with about 200 429s an hour, and it cost only 6 server
  minutes. The skill now names it as an AI crawler.
- Search engines (Applebot, PetalBot, Googlebot, Bingbot) are about 28% of pages and 30% of the
  small server time.
- **One 500:** a WordPress batch-API probe sent `POST /dances/tango-vals/?rest_route=/batch/v1`.
  A POST to a dance page should be a 4xx; the exception is in the App Service logs
  (`analyze-logs`).

#### First day behind the Cloudflare proxy (2026-11-06/07)

22 hours, 2026-11-06 21:00 to 2026-11-07 18:30 UTC, with `www` and `@` proxied and Bot Fight Mode
still off. 137,000 requests, 0.4 server-hours.
- 98% of requests came through Cloudflare. Requests that went straight to the origin dropped to
  about 10 an hour, all crawlers with stale DNS.
- The groups look as before. The headless pattern is still the largest by requests: 46,000,
  mostly assets for pages the farm fetches elsewhere. Applebot is the biggest by server time
  (23%). Moz DotBot sends most of the apex's 20,000 redirects and gets most of the 626
  `CrawlerThrottle` 429s.
- Per-IP sections now show Cloudflare edge addresses (for example a single edge /24 carries
  Exa and the headless farm), so they no longer identify clients. Details and the fix are in
  [cloudflare-migration](cloudflare-migration.md) Phase 4 and section 4.

This day is the before picture for Bot Fight Mode, which went on 2026-11-07.

### 1.2 What a public page costs

The sizes below were measured with `curl` against production.

| Page | HTML (as sent) | HTML gzipped | Of which `model_` | `danceDatabaseJson` |
| --- | ---: | ---: | ---: | ---: |
| `/` | 166 KB | 30 KB | 116 KB (blog entries) | 43 KB |
| `/song` (first page of the catalog) | **1,361 KB** | 148 KB | **1,311 KB** (25 song histories, about 30 KB each) | 43 KB |
| `/song/artist?name=Michael Buble` | 198 KB | 19 KB | | 43 KB |
| `/dances/waltz` | 59 KB | 10 KB | 9 KB | 43 KB |
| `/home/tempi` | 50 KB | 7 KB | | 43 KB |

JS for the `song-index` page is about 185 KB brotli/gzip (`bsvn` 114 KB, the shared vendor chunk
71 KB, `SongTable` 14 KB, `SongLikeButton` 12 KB, and others). CSS is one 295 KB sheet (40 KB
gzipped).

### 1.3 Findings that change the options

These hold whichever rendering option is chosen. Several are cheap to fix.

- **F1. Dynamic HTML is not compressed.** Responses carry no `Content-Encoding`, even with
  `Accept-Encoding: gzip, br`. Static assets are compressed (`MapStaticAssets` precompresses
  them), but HTML isn't: there's no `UseResponseCompression`, and Linux App Service doesn't
  compress dynamic content for you. The HTML in the tables above went out at full size, to
  every bot and every phone. Compressing it alone cuts HTML bytes by about 85–90%.
- **F2. Anonymous HTML is never cacheable, despite what the docs say.**
  [hosting-and-identity](../infrastructure/hosting-and-identity.md#request-pipeline-notes) says
  anonymous `GET 200` HTML gets `Cache-Control: public, max-age=300`. In practice every page
  returns `Cache-Control: no-cache, no-store` plus a `Set-Cookie: .AspNetCore.Antiforgery...`,
  because `_head.cshtml` calls `GetAndStoreTokens` for `menuContext.xsrfToken` on every request.
  The cache-control middleware deliberately skips any response that sets a cookie
  (`M4dApplicationExtensions.cs`, the `Set-Cookie` check), and antiforgery stamps `no-store`
  itself. **This also blocks [plans/front-door-caching](front-door-caching.md) and any output
  caching.** No edge or server cache can hold these pages until anonymous pages stop minting a
  token in the HTML.
- **F3. List pages ship full edit histories.** `SongListModel.histories` holds each song's entire
  `SongProperty` log: about 30 KB per song, 737 KB of JSON for 25 songs, and 1.3 MB once it's
  escaped for the script block. A list row needs a small fraction of that. Album and artist pages
  inherit the same cost.
- **F4. Large page strings land on the large object heap.** Each 100 KB–1.3 MB serialized model
  allocates on the LOH (objects of 85 KB or more), per request. The 2026-10-29 dump baseline
  showed 52 MB of LOH fragmentation (see
  [logging-and-diagnostics](../observability/logging-and-diagnostics.md)). This is a hypothesis,
  not a measurement: shrinking F3 payloads, and so bot-driven LOH churn, is likely worth more
  for memory than any CPU saving.
- **F5. There's no `sitemap.xml`** (it returns 404), and `robots.txt` only disallows
  `/identity/` and Baidu. Crawlers find pages by following links, including search and sort
  permutations.
- **F6. Render-blocking third-party code in `<head>`.** `cookieconsent.min.js` is a synchronous
  script, and the Google Fonts stylesheet has no `display=swap`. Both delay first paint ahead of
  our own code.

## 2. How Google (and other crawlers) treat this site today

This background matters, because it decides which options help ranking at all.

- **Googlebot does render JavaScript.** It uses an evergreen Chromium, so the content is
  indexed; client rendering doesn't make the site invisible to Google. Rendering is a second,
  queued step. Public studies (Vercel/MERJ, 2024) found Google rendering essentially every
  HTML page, with a median delay of seconds but a long tail of hours.
- **The ranking signal tied to rendering is Core Web Vitals**, measured from **real Chrome users**
  (CrUX field data, 75th percentile), not from Googlebot. The thresholds are LCP ≤ 2.5 s,
  INP ≤ 200 ms and CLS ≤ 0.1. So anything that serves fast HTML **only to bots does nothing for
  this signal**. Only what real visitors receive counts. On a mid-range phone, today's LCP waits
  for the HTML (uncompressed, F1), the JS, parsing `model_`, and mounting Vue.
- **Google's position on bot-only rendering.** Google's documentation calls *dynamic rendering*
  (serving bots a prerendered version) "a workaround and not a long-term solution". It
  recommends server-side rendering, static rendering, or hydration. Dynamic rendering isn't
  cloaking **as long as the content is equivalent**. Serving bots different *content* is
  cloaking, and is penalized.
- **Most AI crawlers don't run JS** (GPTBot, ClaudeBot, PerplexityBot and most of the others in
  the sample). They see an empty `<div id="app">` and a JSON blob. If visibility in AI answers
  matters, content in the HTML matters.
- **Bing** renders JS less reliably than Google, and it honors `Crawl-delay` and the crawl
  controls in Bing Webmaster Tools. Google ignores `Crawl-delay`.
- **We haven't confirmed why Google "doesn't like" the site.** Before choosing between the
  Phase 3 options, check Search Console's Core Web Vitals report and its Page indexing report
  ("Crawled, currently not indexed" and "Duplicate without user-selected canonical"
  counts). Thin or duplicate catalog pages, and crawl budget spent on filter permutations, could
  matter as much as rendering speed.

## 3. Constraints

- **Signed-in users keep live client rendering.** Their pages are personalized (votes, tags,
  edit rights, premium), so anything cached or prerendered is anonymous only. Every option
  below keeps that split.
- **Hosting:** a single Linux App Service on the B2 plan (3.5 GB). Memory has been the
  constraint ([background](../observability/logging-and-diagnostics.md)). Anything that adds a
  process (Node, Chromium) must fit alongside the app.
- **One maintainer.** Running two templates per page (Razor and Vue) is a real, ongoing cost,
  not a one-time one.
- **Architecture:** an MPA with 34 Vite entries. Data reaches Vue through window globals
  (`model_`, `menuContext`, `danceDatabaseJson`, `tagDatabaseJson`), and several helpers cache
  them in module-level singletons (`getMenuContext()`). See
  [frontend-architecture](../pages/frontend-architecture.md).
- **Cloaking:** whatever bots receive must carry the same content a human would see.
- **There is no non-JS rendering today.** Without JS, a visitor or crawler gets an empty
  `<div id="app">`. The page data exists only as JSON inside a `<script>` block, which crawlers
  can scrape but nothing displays. Any HTML version therefore takes one of two forms: a
  separately written page per page type (C, D), or the existing Vue templates rendered on the
  server (E, F). Only the second avoids maintaining two versions of each page.

## 4. Options

Options A and B reduce cost without changing how pages render. Options C to I change what
anonymous visitors and bots receive. They aren't all mutually exclusive: the recommendation in
section 6 combines several.

### A. Quick wins with no rendering change

| Item | Effect on bots | Effect on SEO / CWV | Effort |
| --- | --- | --- | --- |
| A1. HTML response compression (`AddResponseCompression`, brotli + gzip, `EnableForHttps`) | About 85–90% less HTML bandwidth | Faster HTML download on mobile, so better LCP | Small |
| A2. Slim list models (F3): send list rows only the properties they render, and load full history on the details page | Large: album/artist/list pages shrink several-fold, with less serialization and LOH allocation | Faster parse and mount, better LCP and INP | Medium (client `Song` construction from a trimmed history must be checked) |
| A3. Keep the antiforgery token out of anonymous HTML (F2): mint it lazily from a small `GET` endpoint the first time an anonymous page needs to `POST` (usage-log batches, suggestions) | None directly, but it **unblocks** every caching option | Same: unblocks caching | Medium (touches `menuContext`, `getAxiosXsrf`, usage tracking, and the 1-day cookie decision) |
| A4. `robots.txt` + `sitemap.xml`: disallow search and sort permutations (`/song/sort`, `/song/filtersearch`, `/song/azuresearch`, `/song/advancedsearchform`, `/song/Advanced`); publish a sitemap of canonical dance, song, album and artist URLs with `lastmod`; `noindex, follow` on permutations | Well-behaved crawlers stop walking permutations | Crawl budget goes to canonical pages | Small for robots; medium for a generated sitemap of tens of thousands of songs |
| A5. Defer render-blocking third parties (F6): load `cookieconsent` with `defer`, add `&display=swap` to the Google Fonts URL, consider self-hosting the two fonts | None | Earlier first paint and LCP | Small |
| A6. Throttle crawler classes: a per-user-agent-class rate limit on catalog pages (for example N/minute for anything matching `ExcludeFragments` that isn't a verified search engine), answered with `429` + `Retry-After`. Use Bing Webmaster crawl control too | Caps the worst offenders | None (leave Googlebot alone) | Small–medium (extends `SpiderManager` and the rate-limit middleware) |
| A7. Investigate the slow outliers (album-by-title with special characters, deep `page=` values) | Removes 10–50 s requests | None | Small |

A1, A4 and A5 are the cheapest. A2 and A3 matter most, because A3 is the gate for everything
in B and beyond.

**Why A3's token endpoint is safe.** Any site can make a browser *send* a `GET` to the
endpoint, but the same-origin policy stops that site from *reading* the response. Without the
request token, the antiforgery cookie can't validate a forged `POST`. This is the same exposure
as today: every HTML page is already a `GET` that mints a token in `menuContext`. That holds
only if the endpoint:

1. returns JSON (`application/json`), never script, since a `<script src>` include would leak a
   `var token = ...`;
2. never gets a permissive CORS policy (the app has no CORS configuration today);
3. is `Cache-Control: no-store` and is excluded from every cache, Front Door included, or one
   cookie/token pair would be served to everyone;
4. has no effect besides issuing the token;
5. is fetched again after sign-in or sign-out, because tokens are bound to the user's identity.
   Signed-in pages can keep the token in the HTML.

Like any CSRF token scheme, this does nothing against XSS.

### B. Caching anonymous HTML (needs A3 first)

- **B1. Server output caching** (ASP.NET Core `OutputCache`) for anonymous `GET`s, varying by
  path and query and bypassed when an auth cookie is present. It's cheap to add. The catch is
  memory: the cache lives in the same 3.5 GB process, so it needs a size limit, and it's a poor
  fit for 1 MB pages until A2 lands.
- **B2. Edge caching with Azure Front Door**, as already planned in
  [plans/front-door-caching](front-door-caching.md). It adds WAF and bot rules, at a fixed
  monthly base fee plus usage.
- **B3. Edge caching with Cloudflare** in front of App Service (DNS moves to Cloudflare). The free
  or Pro tier includes caching, Bot Fight Mode, verified-bot identification, and AI-crawler
  blocking and controls. That's often more bot tooling per dollar than Front Door Standard. The
  costs: a second vendor, and the client-IP/forwarded-header work noted in the Front Door plan.
  Details below.

#### B3 in detail: what moving to Cloudflare involves (2026-10-31)

Today the domain's DNS is at Namecheap. `music4dance.net` and `www.music4dance.net` use free
App Service managed certificates (GeoTrust, auto-renewed by Azure), and the app 308-redirects
the apex to `www`. Mail is Microsoft 365 (MX `music4dance-net.mail.protection.outlook.com`).

- **Certificates.** A proxied request has two TLS legs. Visitor to Cloudflare uses Cloudflare's
  Universal SSL certificate, which Cloudflare issues and renews on its own. Cloudflare to App
  Service still needs a certificate on the app, checked by setting the SSL mode to **Full
  (strict)**. The App Service managed certificate is the risk: Azure renews it only while the
  hostname's DNS points straight at `azurewebsites.net`, and a proxied record points at
  Cloudflare. Reports of silent renewal failures behind the proxy are common (not confirmed in
  Microsoft's docs). Use a **Cloudflare Origin CA certificate** instead: free, valid up to 15
  years, trusted only by Cloudflare, uploaded to App Service once. That fits, since the origin
  should only accept Cloudflare anyway.
- **DNS.** Every record moves to Cloudflare, each marked proxied (orange cloud) or DNS only
  (grey cloud). Before switching nameservers, check the import caught the MX, SPF and DKIM
  records and any `asuid` TXT verification records; mail records stay DNS only. Cloudflare
  flattens a CNAME at the apex.
- **Subdomains can point anywhere.** For the planned API, `api.music4dance.net` as a **DNS-only**
  CNAME to the Azure Function's hostname leaves Cloudflare out of the path, and Azure's free
  certificate works because the CNAME really does point at Azure. If it's proxied instead, note
  that Free's Bot Fight Mode applies to the whole zone and can't be skipped, so it could
  challenge legitimate API clients (which are scripts by definition); Pro's Super Bot Fight Mode
  can be skipped by a WAF custom rule (verify before relying on it).
- **App changes:** read the client IP from `CF-Connecting-IP`, and restrict App Service access
  to Cloudflare's published IP ranges (there's no Azure service tag for them).

#### Edge options compared (2026-10-31)

Volumes, from the HTTP logs: about 230,000 requests and 3.8 GB a day after Phase 1, so roughly
**7 million requests and 115 GB a month**. A day like 2026-10-31 with the scraping wave was
about 460,000 requests and 6.5 GB, roughly **14 million and 200 GB a month** if it were every
day. List prices, North America:

| | Front Door Standard | Front Door Premium | Cloudflare Free | Cloudflare Pro | Cloudflare Business |
| --- | --- | --- | --- | --- | --- |
| Base | $35/month | $330/month | $0 | $20/month annual, $25 monthly | $200 annual, $250 monthly |
| Usage | $0.083/GB to clients, $0.009 per 10,000 requests | $0.083/GB, $0.015 per 10,000 requests | not billed by bandwidth or requests | same | same |
| Estimate, typical month | about $50 | about $350 | $0 | $20–25 | $200–250 |
| Estimate, wave every day | about $65 | about $370 | $0 | $20–25 | $200–250 |
| How it scales | +$8.30 per extra 100 GB, +$9 per extra 10M requests | +$8.30 per 100 GB, +$15 per 10M requests | flat | flat | flat |
| Bot tooling | WAF custom rules only (IP rate limits, geo, header matches) | adds the Bot Manager rule set and the JavaScript challenge action | Bot Fight Mode (simple bots, headless browsers; not adjustable) | Super Bot Fight Mode with JavaScript detections, adjustable | adds detection of more sophisticated bots |
| AI-crawler controls | custom rules by user agent | same | AI Crawl Control (allow/block per crawler) | same | same |
| Certificates | managed, auto-renewed for `www` (CNAME); see the Front Door plan for the apex | same | Universal SSL edge cert plus a 15-year Origin CA cert | same | same |
| DNS | stays at Namecheap | same | moves to Cloudflare | same | same |

Notes on the comparison:

- **Billing source.** The subscription is Visual Studio Enterprise, so Front Door's charges
  would come out of its monthly Azure credit; Cloudflare is a separate card charge.
- **Price as a lever against floods.** A flood of requests raises a Front Door bill a little
  (14 million requests is about $13 on Standard). Cloudflare's DDoS protection is unmetered.
- **Neither is guaranteed against the 2026-10-31 wave.** It runs JavaScript, so a JavaScript
  challenge alone may not stop it; what helps is fingerprinting automation (headless signals,
  TLS fingerprints), which is what Cloudflare's bot modes do and Front Door Standard doesn't.
  Per-IP rate limits on either platform don't touch 54,000 IPs.
- **At equal bot capability, Cloudflare Pro costs about a fifteenth of Front Door Premium.**
  Front Door Standard is the choice that keeps everything in Azure with the least to manage
  ([plans/front-door-caching § 2](front-door-caching.md#2-a-low-maintenance-setup-that-stays-in-azure-2026-10-31)),
  but it gets caching, not bot detection.

**A caveat that matters here:** bots crawl the *long tail* (each song or album page is fetched
rarely), so caching cuts origin load for popular pages much more than for crawler traffic. For
bots, cheaper responses (A1, A2, and C to F below) and demand control (A4, A6, B3's bot rules)
matter more than hit rate. Caching mainly helps humans and makes the edge bot tools available.

### C. Separate lightweight HTML for bots (dynamic rendering, Razor templates)

Detect crawler user-agents (`SpiderManager.CheckAnySpiders` already does) and return a
**separate page**: a Razor view with the page's core content as plain semantic HTML (title,
description, song table with title/artist/dances/tempo, links). It has no Vue bundle, no
`model_` and no dance database. Since nothing renders without JS today, this is new markup for
every page type it covers, not a trimmed copy of something that exists.

- **Bots:** a large win for bots that fetch HTML only. Pages shrink to a few KB to a few tens of
  KB, and bots that would have run the JS (Reflectionbot, Applebot) don't fetch 200 KB of
  bundles. The server still runs the same queries, so the CPU saving is mostly serialization.
- **SEO:** content becomes visible to non-rendering crawlers, including AI crawlers. **It does
  nothing for Core Web Vitals**, which are measured from real users. Google tolerates it but
  documents it as a workaround.
- **Risk:** the bot templates drift from the Vue pages over time. If they diverge in content,
  that's cloaking. Bot detection by user-agent is also leaky: disguised bots still get the
  expensive page, and a real user with an odd UA gets the stub.
- **Cost:** one extra Razor view per page type covered. Five page types (song details, album,
  artist, dance details, the list stub) would cover most bot bytes.
- **Verdict:** a fast, self-contained fix for problem 1 only. Worth it as a stopgap if Phase 3
  slips, especially for album and artist pages. Long term, prefer an option where bots and
  humans get the same HTML.

### D. Razor-rendered content shell for every anonymous visitor (progressive enhancement)

Razor renders the core content (the same semantic HTML as C) **inside `<div id="app">` for all
anonymous visitors**, and the Vue app mounts over it and takes over. One HTML response serves
both bots and humans, so there's no cloaking question. Bots that don't run JS get real content,
and humans see content before the JS finishes. As with C, the Razor markup is a second,
hand-written rendering of each page, kept in step with its Vue version.

- **Bots:** they get content in the HTML, but they still download `model_` unless the page also
  serves a no-script variant to crawlers. The `model_` cost is limited once A2 lands.
- **SEO:** real LCP improvement, because the largest element (the heading or song table) paints
  from HTML.
- **Risk:** Vue's `mount()` replaces the server markup rather than hydrating it, so a visible
  swap (CLS) happens unless the Razor markup closely matches the Vue output. That means keeping
  the classes and structure aligned by hand.
- **Cost:** the same duplicate-template burden as C, and the templates must match visually,
  which is stricter.
- **Verdict:** a reasonable choice if true SSR turns out impractical (see E). It's the
  "server-rendered first, Vue enhances" model, applied to the handful of page types that matter.

### E. True Vue SSR with hydration, anonymous only

Render the existing Vue components to HTML on the server, inject it into `<div id="app">`, and
have the client call `createSSRApp(...).mount()` to **hydrate** the server markup instead of
replacing it. One template per page serves both humans and bots. This is Google's preferred
approach. Signed-in users keep the current client-only path.

**How it would fit this codebase:**

- **Build:** add a Vite SSR build (`vite build --ssr`) that emits a server bundle exporting
  `render(pageName, model, menuContext, danceDb, tagDb) => html`. `AutoEndpoints` already
  knows every page, so it can generate the page registry for the server entry the same way it
  generates the client `main.ts` files.
- **Globals refactor (the main cost):** `model_`, `menuContext`, `danceDatabaseJson` and
  `tagDatabaseJson` are read from `window`, and `getMenuContext()` / `safeDanceDatabase()`
  cache them in module-level singletons. In SSR, module state is shared across requests, so
  these must move to per-app `provide`/`inject`. The client would read them from the same
  injected source, so the client entry keeps working.
- **Component audit:** anything touching `window`, `document`, `localStorage`, timers or
  `Date.now()` during setup has to move to `onMounted`, or the server and client markup
  disagree (hydration mismatch). bootstrap-vue-next is SSR-capable (it has a Nuxt module), but
  modals, toasts and popovers are client-only. The Spotify player, usage tracking and the
  engagement offcanvas are client-only by nature.
- **Where the renderer runs**, which is the main hosting decision:
  - **E1. Node sidecar.** A small Node HTTP service next to the app (App Service sidecar
    container, or a separate container) that ASP.NET posts page data to. It's the conventional
    path with the best tooling, but it's another runtime to deploy, monitor and fit into memory.
    Check current App Service sidecar support for code-based (non-container) Linux apps before
    choosing it.
  - **E2. In-process V8 via Microsoft ClearScript.** `@vue/server-renderer`'s `renderToString`
    is plain JavaScript (it also runs in edge workers), so the server bundle can run inside the
    .NET process on V8, with no Node to deploy. The native V8 binaries ship as a NuGet package
    for linux-x64. The V8 heaps count against the app's memory, and this route is less traveled
    than Node.
  - **E3. Edge rendering** (Cloudflare Workers or similar). It fetches page data as JSON from the
    origin and renders at the edge, which takes render CPU off the App Service. It needs a JSON
    data endpoint per page type and B3's vendor move, and it's the most moving parts.
- **Payload:** hydration needs the state as well as the markup, so SSR HTML is *larger* than
  today's (rendered markup plus `model_`). For bots, the same renderer can emit a **no-script
  variant** (identical markup, no bundle, no state). That gives C's bot savings with no second
  template, and since the content is identical, it's not cloaking. This variant is the Vue
  templates' own server output, not a separate page, and it exists only once the SSR work
  lands. In it, links and text work, but interactive controls (vote, play, tag editing) do
  nothing until JS runs.
- **Rendering cost:** SSR adds CPU per anonymous request (rendering a 25-row `SongTable` is
  tens of milliseconds), so it has to be paired with caching (B) to come out ahead on load.
- **Verdict:** the best end state for both problems, and the biggest investment. De-risk it with
  a one-page spike before committing (section 6, Phase 3).

### F. Static pre-rendering (SSG) of the bounded page set

Build-time or scheduled pre-rendering works for pages whose content changes rarely and whose
count is bounded: home, dance index and dance details (about 100 dances and styles),
competition categories, tempo list, FAQ, wedding pages. Render with E's server bundle (or a
headless browser at build time), write the HTML to `wwwroot` or Blob storage, and serve it to
anonymous visitors. It could regenerate on the existing 6-hourly `DanceStats` rebuild.

- It doesn't scale to the song, album and artist long tail (tens of thousands of pages, and
  those are what bots hit most). "Render on first request, store with a TTL" (incremental static
  regeneration) is really E plus B under another name.
- **Verdict:** a good first deliverable *after* the E spike, because these pages have small
  models and few interactive parts. It isn't a standalone answer to bot load.

### G. Headless-browser prerendering service

Bots are routed to a prerender service (prerender.io, or self-hosted Puppeteer/Chromium) that
loads the real page in a browser and returns the resulting DOM. Rendertron, Google's own, is
archived.

- Hosted services charge by renders per month, and this site's crawl volume would be a
  meaningful bill. Self-hosting Chromium on a memory-constrained B2 is a non-starter.
- It's dynamic rendering, with C's "workaround" status and none of E's CWV benefit.
- **Verdict:** not recommended.

### H. Move to a full SSR framework (Nuxt, or Astro with Vue islands)

Nuxt gives SSR, hybrid rendering, caching rules and islands out of the box. Astro renders
static HTML and hydrates only interactive "islands", which fits this site's mostly-content
pages well.

- Either one moves routing and page data loading out of ASP.NET MVC into a Node front end, which
  calls the .NET app as an API. That's effectively a rewrite of the page layer: 34 pages, plus
  auth, antiforgery and premium gating across two runtimes.
- **Verdict:** not recommended at this scale. E gets most of the benefit and keeps MVC.

### I. Islands within the current stack (longer-term direction)

Razor renders the page's static content, and Vue mounts only the interactive pieces: vote
buttons, play buttons, tag editors, menus. That's the Astro model without Astro. It reverses
the "Vue renders the whole page" decision for content-heavy pages, so it's a gradual direction,
not a project. It's mentioned here because D, applied to many pages over time, converges on it.

## 5. Comparison

Ratings are relative within this plan: ++ large, + some, 0 none, − worse.

| Option | Bot bandwidth | Bot server cost | Content visible to non-JS crawlers | Real-user CWV | Effort | Ongoing maintenance | Memory risk on B2 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A1 compression | ++ | 0 | 0 | + | S | none | none |
| A2 slim models | ++ | + | 0 | + | M | low | improves |
| A3 token out of anonymous HTML | 0 (enables B) | 0 | 0 | 0 | M | low | none |
| A4 robots + sitemap | + | + | 0 | 0 | S–M | low | none |
| A5 defer third parties | 0 | 0 | 0 | + | S | none | none |
| A6 crawler throttling | + | + | 0 | 0 | S–M | low (lists) | none |
| B1 output cache | + (hot pages) | + | 0 | + | S | low | − without size cap |
| B2/B3 edge cache + bot rules | + | ++ (with bot rules) | 0 | + | M | low | none |
| C bot-only Razor | ++ | + | ++ | 0 | M | **high** (drift) | none |
| D Razor shell for all anonymous visitors | + | 0 | ++ | ++ | M–L | **high** (must match Vue) | none |
| E Vue SSR + hydration | ++ (no-script variant) | − alone, + with B | ++ | ++ | L | low (one template) | − (V8/Node) |
| F SSG of the bounded set | + | + | ++ | ++ | M (after E) | low | none |
| G prerender service | ++ | 0 | ++ | 0 | M | medium + bill | − if self-hosted |
| H Nuxt/Astro | ++ | + | ++ | ++ | XL | rewrite | − |

## 6. Recommended path

The phases are ordered so that each one pays off on its own and none blocks on a decision that
isn't ready yet.

### Phase 0: Measure (days)

- Analyze **a full day** (ideally a weekday and a weekend day) of HTTP logs with the same
  classification as section 1.1. Record the baseline: bot share of requests, bytes and time;
  top crawlers; top page types. **Weekday done 2026-10-30** (section 1.1, "Baseline");
  **post-deploy rerun done 2026-10-31** (section 1.1, "After Phase 1 deployed"). A clean weekend
  day is still missing, since the 2026-10-31 wave swamps that day's numbers.
- Pull **Search Console**: the Core Web Vitals report (which URL groups fail, and on which
  metric), the Page indexing reasons, and Crawl stats (requests per day, average response size
  and time). This answers "what does Google dislike" before Phase 3 commits to a direction.
- Run PageSpeed Insights or Lighthouse (mobile) on `/`, `/dances/waltz`, one song details page,
  one album page and `/song` to get a lab baseline. The anonymous PSI API quota was exhausted
  on 2026-10-29, so use an API key or the web UI.
- Optionally, add `Server-Timing` headers (query vs. serialization vs. view) on the catalog
  actions, to see where bot server time actually goes.

### Phase 1: Quick wins (a few small PRs)

In rough order of value per effort: **A1** compression (for anonymous responses first; see the
note on BREACH below), **A5** defer third parties, **A4** robots/sitemap, **A7** slow outliers,
**A2** slim list models, **A6** crawler throttling, then **A3** token out of anonymous HTML.
Decide the AI-crawler policy (open question 1) as part of A4/A6.

BREACH note for A1: compressing HTTPS responses that contain a secret and reflected input is
the BREACH attack surface. ASP.NET Core's protected request tokens are re-encrypted with a fresh
IV on each response, which defeats the attack's assumption of a stable secret. A3 also removes
the token from anonymous HTML altogether. Confirm both before enabling compression for
authenticated responses.

At the end of Phase 1, re-run the Phase 0 measurements. Bot bandwidth should fall by an order
of magnitude, and it may turn out that bot load no longer needs anything beyond A6.

**Phase 1 as built (2026-10-29, deployed 2026-10-30 about 23:00 UTC in PR #365).** Each step
is one commit on `bot-load-quick-wins`, and each reference doc describes its piece. The same PR
added `CrawlerThrottle:AgentLimits`, the `/searches` robots rule and the 404s for injection
probes (section 1.1). The results are in section 1.1, "After Phase 1 deployed": bot bandwidth
fell by about two thirds rather than the order of magnitude hoped for; details pages, which
A2 doesn't trim, are now the largest share.

| Step | What shipped | Reference |
| --- | --- | --- |
| A1 | Brotli/gzip for anonymous responses only (`AnonymousCompressionMiddleware`; authenticated pages stay uncompressed). `/dances/waltz` went from 56 KB to 11 KB. | [hosting-and-identity](../infrastructure/hosting-and-identity.md#request-pipeline-notes) |
| A2 | `SongHistory.TrimAlbumsForList` for anonymous list pages: drops album properties a row can't reach. 52 production songs went from 1.10 MB to 0.45 MB with identical rows. | [song-search-results](../search/song-search-results.md) |
| A3 | No antiforgery token in anonymous HTML; `GET /api/antiforgery/token` plus `AntiforgeryToken.ts`. The anonymous cache-control branch sends `no-cache`, not `public` (see below). | [frontend-architecture](../pages/frontend-architecture.md#menucontext-per-request-user-and-site-state) |
| A4 | `robots.txt` disallows search permutations and `/api/`; `/sitemap.xml` (80 site-map pages); the `/song` bot stub is `noindex, follow`. | [bot-and-abuse-defense](../security/bot-and-abuse-defense.md), [blog-help-sitemap](../pages/blog-help-sitemap.md) |
| A5 | `defer` on cookie consent; `display=swap` and preconnects for Google Fonts. | [frontend-architecture](../pages/frontend-architecture.md) |
| A6 | `CrawlerThrottleMiddleware`: 20 requests/minute per crawler on catalog paths, search engines exempt, shown on `/Admin/Diagnostics`. `AgentLimits` adds crawlers BotFilter misses and per-agent limits (Reflectionbot 3/minute). | [bot-and-abuse-defense](../security/bot-and-abuse-defense.md) |
| A7 | `FindByField` capped at 1,000 results, plus an exact-match album filter. The cause was unbounded SDK paging: `÷ (Deluxe)` matched 4,259 songs. | [artist-pages](../songs/artist-pages.md) |

Learned along the way, and needed before Phase 2:

- **Making anonymous HTML cacheable is more than removing the cookie.** `UseResponseCaching` is
  in the pipeline (for the tag and dance-environment APIs). It doesn't look at cookies, so a
  `public` anonymous page would be served from memory to signed-in users, and a browser would
  show the signed-out page after login. The anonymous branch therefore sends `no-cache`. Phase 2
  needs a cache that keys on sign-in state (ASP.NET Core `OutputCache` with a policy that skips
  authenticated requests, or an edge rule on the identity cookie), and only then `public`.
  Anonymous pages also vary by user agent today: `_head.cshtml` drops the AdSense script for
  Googlebot, and a shared cache would hand that variant to people too.
- **The legacy `Usage` cookie** (server-side usage logging, only while `ClientSideUsageLogging`
  is off) also makes a page uncacheable. Production has the flag on.
- **Deep catalog paging is slow** (`/Song?page=1584` took 10 s) because Azure Search's cost
  grows with `skip`, and Azure rejects a `skip` over 100,000. The bot stub tells crawlers to
  page through `/song`, so it wasn't capped. A song sitemap (open question 5) would let
  crawlers reach songs without deep paging, and then paging could be capped.
- **A7, checked end to end (2026-10-30)** with the real app against `songs-test-3`, compared with
  production's old code. `÷ (Deluxe)` took 2.0 s instead of 5.1 s, and showed 14 songs instead
  of 15. The missing song was a false match in the old code: the title matcher's fallback strips
  punctuation, so Ed Sheeran's `=` and `÷ (Deluxe)` both reduce to an empty string.
  "Greatest Hits" took 1.2 s instead of 8.5 s, with 784 songs instead of 787. It has 1,343 exact
  and 4,585 phrase matches, so the 1,000 cap on each query drops a few; paging the exact query
  would recover them if that page matters. Smaller albums matched exactly. (The local app
  couldn't reach search at first because `AZURE_CLIENT_ID` in user secrets had a stray
  character.)

**Follow-up: field-driven history pruning (generalizes A2).** Not scheduled. A2 trims one kind
of property, album data a list row can't reach, with hand-written rules that mirror the client's
`buildAlbumInfo`. The general version prunes a history down to the properties that actually
produce what a view renders:

- **Input:** the fields the view shows, with an index for array fields (for example `Title`,
  `Artist`, `Tempo`, `DanceRating`, `Album:0`, plus the first Spotify and iTunes purchases). A
  list row, the details page and the API could each declare their own set.
- **Tracking:** `Song.Load` / `LoadProperties` (`m4dModels/Song.cs`) already replays the log to
  build the song. With a field list, it would also record which property *instances* (the
  actual entries in the log, not their types) produced each field's final value. For a
  last-writer field such as `Title`, that's the last write that took effect (bot values
  don't overwrite user values). For accumulating fields such as dance ratings, tags and votes,
  it's every entry that still counts after removals.
- **Output:** the history filtered to those instances. Edit headers (`.Create`/`.Edit`, `User`,
  `Time`) stay, so `modified`, `edited` and `modifiedBy` are unchanged; drop a header only when
  its block is empty and those values aren't in the field list either.
- **Gain:** beyond A2's albums, this removes superseded values, such as the title and artist
  renames that are common in the catalog, and every property no rendered field uses. A2
  becomes one case of it.
- **Risk:** the client replays the pruned log itself, so the server's notion of "the entry that
  produced this value" must match the client's replay exactly. That covers the bot-versus-user
  overwrite rule, per-dance tempo promotion, artists and deletions. Verify it the way A2 was:
  shared C#/TypeScript fixtures (like `album-trim-cases.json`) plus an equivalence run over a
  production sample. Keep it to anonymous viewers, as A2 is, because signed-in users edit from
  the full log.

### Phase 2: Caching (after A3)

Choose between **B1** alone (cheap, server-side, size-capped) and an edge (**B2** Front Door, or
**B3** Cloudflare, which leads on bot tooling per dollar). Update
[plans/front-door-caching](front-door-caching.md) with the outcome, either way.

What the 2026-10-31 data adds:

- **B1 is now possible but modest.** A3 removed the per-request token, so anonymous pages no
  longer set a cookie. The remaining per-request differences an `OutputCache` policy has to
  handle: the AdSense script is left out for Googlebot (`_head.cshtml`), and `IsSpider` agents
  get the `/song` bot stub, so the cache key needs a bot/Googlebot flag; the default policy
  already skips signed-in requests and responses that set a cookie. Memory is bounded by the
  store's `SizeLimit` (100 MB by default). Expected hit rate is about 13–20% (section 1.1), and
  the request locking in `OutputCache` would collapse the wave's duplicate requests within a
  burst, but not its first fetch of each distinct page.
- **The edge decision is now about bot management more than caching.** The wave is the kind
  of traffic an origin can't separate from people: tens of thousands of residential IPs, a
  current-looking Chrome, real JS execution. Per-IP rate limits (Cloudflare's included) don't
  touch it. Edge bot detection that fingerprints headless browsers (Cloudflare Bot Fight Mode
  or Pro's Super Bot Fight Mode, Front Door's WAF bot rules) is the realistic answer.
- **An origin-side stopgap** if bursts keep stalling the site before an edge is in place: a
  concurrency limit on anonymous catalog pages for requests that carry none of the site's
  cookies, which sheds a burst quickly with 503s instead of letting every request slow to 3–4
  s. First-time visitors would also be refused during a burst, so this is a trade-off.

### Phase 3: Content in the HTML

1. **Spike E on one page** (`dance-details`: modest model, real interactivity, high SEO value).
   Prove out the server bundle, the globals-to-`provide`/`inject` refactor, bootstrap-vue-next
   hydration, and one renderer host (E2 ClearScript first, because it needs no new runtime;
   E1 Node if E2 fails). Measure render time and memory.
2. **Decide.** If the spike is clean, roll E out to the bot-heavy, SEO-relevant page types in
   this order: dance details and the other bounded pages (F becomes trivial here), song details,
   album, artist, then the song list. Bots get the no-script variant. If the spike exposes deep
   SSR incompatibilities, fall back to **D** for the same page list.
3. Only if Phase 3 is far off and album/artist bot load is still hurting after Phase 1: build
   **C** for album and artist pages as an explicitly temporary stopgap, and plan to retire it
   when E or D lands.

Not recommended: **G** (cost, and no real-user benefit) and **H** (a rewrite).

## 7. Open questions for the owner

1. **AI crawlers:** allow, throttle or block GPTBot, ClaudeBot, PerplexityBot, Exa, ElevenLabs,
   Reflection and the rest? Blocking them cuts load, but removes the site from AI answers that
   may send dancers its way. This is a business decision, not a technical one. In the full-day
   sample, AI crawlers took 32% of server time (Reflectionbot alone 22%), and 7 people arrived
   from search engines other than Google, none from an AI assistant.
   **Decided 2026-10-30: allow, and throttle.** The owner sees human traffic that starts from AI
   answers (it doesn't show up as an AI referrer, since assistants rarely send one), so AI
   crawlers stay open; the heavy ones get a per-agent limit in `CrawlerThrottle:AgentLimits`.
2. **Search Console data:** which URL groups fail Core Web Vitals, and on which metric? Are a
   large share of song pages "Crawled, currently not indexed"? The answer could move Phase 3
   ahead of Phase 2, or push it back.
3. **Edge vendor:** is moving DNS to Cloudflare acceptable, or is the preference to stay
   all-Azure (Front Door)? As of 2026-10-31 the owner is weighing Cloudflare over Front Door, and the
   residential-proxy wave (section 1.1) makes edge bot detection more pressing than caching.
4. **Runtime appetite:** is a Node sidecar acceptable to run, or should SSR stay in-process (E2),
   or not happen (D)?
5. **Song-page indexing strategy:** should every song page be indexable, or should thin songs
   (few votes, no tempo) be `noindex, follow` the way thin artist pages already are? That
   changes how much of the long tail any of this needs to cover.

## Related

- [frontend-architecture](../pages/frontend-architecture.md): how Vue pages are built and how data reaches them
- [bot-and-abuse-defense](../security/bot-and-abuse-defense.md): `SpiderManager`, the `BotFilter` stub, crawler short-circuits
- [plans/front-door-caching](front-door-caching.md): edge caching, blocked by F2
- [plans/attack-mitigation-phase2](attack-mitigation-phase2.md): WAF and edge bot rules
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): the cache-control middleware (its `public, max-age=300` description doesn't hold in practice; see F2)
- [usage-tracking](../observability/usage-tracking.md): client-side usage logging, which already works for cached pages
- [runbooks/read-production-logs](../runbooks/read-production-logs.md): getting the HTTP logs used in section 1.1
