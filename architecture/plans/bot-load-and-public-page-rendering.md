# Bot Load and Public-Page Rendering Options

**Type:** Plan
**Status:** Proposed (options survey; no decision yet)
**Last verified:** 2026-10-29
**Code:** `m4d/Views/Shared/Vue3.cshtml`, `m4d/Views/Shared/_head.cshtml`, `m4d/Views/Shared/_vue-Layout.cshtml`,
`m4d/Configuration/M4dApplicationExtensions.cs` (cache-control middleware), `m4d/Utilities/SpiderManager.cs`,
`m4d/Controllers/SongController.cs`, `m4d/ClientApp/vite.config.ts`

This plan covers two problems that share a cause. Every public page is an empty `<div id="app">`
plus a large inline JSON model, and the browser has to build the page:

1. **Bot load.** Crawlers (search engines, AI crawlers, SEO tools, scrapers) make most of the
   page requests, and each one gets the full client-rendered payload.
2. **Search ranking.** Google indexes the site, but client rendering costs a lot of page-load
   performance (LCP), and many non-Google crawlers never see the content as HTML.

It records what was measured on 2026-10-29, sets out the options broadly (including ones this
plan doesn't recommend, so they don't need re-researching), compares them, and proposes a
phased path. Signed-in users stay on today's live client rendering in every option. Only the
anonymous experience changes.

## 1. What we measured (2026-10-29)

### 1.1 Traffic mix

Source: the App Service HTTP logs in storage `m4dlogs`
([runbooks/read-production-logs](../runbooks/read-production-logs.md)), **one 22-minute window**
(21:50–22:12 UTC, 3,210 requests). Bots were classified by user-agent. Treat the numbers as an
indication until a full day is analyzed (Phase 0).

| Class | Page requests | HTML bytes out | Server time (sum of `TimeTaken`) |
| --- | ---: | ---: | ---: |
| Self-identified bots | 905 (91%) | 70.0 MB (80%) | 91.5 s (93%) |
| Browser user-agents | 90 | 16.5 MB | 6.8 s |
| App Service Always On | 6 | 0.9 MB | 0.1 s |

- **Some of the "browser" traffic is bots too.** 56 of the 90 browser-UA page requests came from
  IPs that never fetched a single JS or CSS asset, so likely about 35 of those page views were
  human.
- **Top bots by page requests:** DotBot (522, mostly `/song/sort` permutations that already get
  the cheap `BotFilter` stub), Reflectionbot (134), PetalBot (98), ExaSearchBot (43), bingbot (22),
  Applebot (21), ElevenLabsCrawler (20), Googlebot (15).
- **The expensive targets are album, details and artist pages.** `/song/album` drew 119 bot hits
  that averaged **270 KB** each (the largest was 1.5 MB) and accounted for 32 MB of the 70 MB.
  Next came `/song/details/*` (90 hits, 17 MB) and `/song/artist*` (35 hits, 6 MB). AI and
  answer-engine crawlers (Exa, ElevenLabs, Reflection, Perplexity, DuckAssist) hit these heavily.
  These pages are only ever blocked for the narrow `BadFragments` list.
- **Few bots run the JS.** Reflectionbot (1,023 asset requests), DuckAssistBot and Applebot load
  the bundles. Most others take only the HTML, so for them the content is just the JSON inside
  `var model_ = ...`.
- **Slow outliers:** bingbot on `/song/album?title=÷ (Deluxe)` took **49.9 s** and got 0 bytes (a
  timeout), and `/song/Advanced?...&page=1584` took 10.3 s. Both are worth a look on their own.
- **Absolute load in this window was modest.** Average concurrency was well under one request.
  Bots are most of the traffic, but this sample doesn't show them overwhelming a core. The
  October out-of-memory restarts correlated with a platform base image, not with traffic. The
  memory angle is in section 1.3.

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
  top crawlers; top page types.
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

### Phase 2: Caching (after A3)

Choose between **B1** alone (cheap, server-side, size-capped) and an edge (**B2** Front Door, or
**B3** Cloudflare, which leads on bot tooling per dollar). Update
[plans/front-door-caching](front-door-caching.md) with the outcome, either way.

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
   may send dancers its way. This is a business decision, not a technical one.
2. **Search Console data:** which URL groups fail Core Web Vitals, and on which metric? Are a
   large share of song pages "Crawled, currently not indexed"? The answer could move Phase 3
   ahead of Phase 2, or push it back.
3. **Edge vendor:** is moving DNS to Cloudflare acceptable, or is the preference to stay
   all-Azure (Front Door)?
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
