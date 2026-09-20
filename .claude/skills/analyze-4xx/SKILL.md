---
name: analyze-4xx
description: >-
    Triages the production 4xx URL export from /Admin/Diagnostics — separates
    scanner noise from real music4dance bugs, widens the attack filter, and
    updates the triage log. Use when the user asks to analyze 4xx/404 errors,
    review the error export, look at the diagnostics 4xx table, or mentions a
    4xx-urls-*.csv file.
---

# analyze-4xx

Triages the 4xx URL export produced by `/Admin/Http4xxExportCsv`. The goal of
every pass is the same three outputs:

1. **Real music4dance bugs** found and fixed (broken internal links, bad URL
   construction, missing well-known files).
2. **New scanner patterns** added to `Http4xxTracker.IsKnownAttackUrl`, so the
   next export is quieter and the signal stands out.
3. **A row in the triage log** for every pattern seen, so nothing gets
   re-investigated from scratch next time.

## The moving parts

| Thing | Where |
| --- | --- |
| Filter (`IsKnownAttackUrl`) | `m4d/Security/Http4xxTracker.cs` |
| Filter tests | `m4d.Tests/Security/Http4xxTrackerTests.cs` |
| Recording middleware | `m4d/Middleware/Http4xxTrackingMiddleware.cs` |
| CSV export action | `AdminController.Http4xxExportCsv` |
| **Triage log (read this first)** | `architecture/distributed-attack-mitigation.md` → "Known 404/4xx Sources (Triage Log)" |

## Step 1 — Get the CSV

Look in `local/` for `4xx-urls-*.csv` first; the user often downloads it ahead
of time. Use the newest one and say which file and date you are working from.

If there isn't one, offer to fetch it. The export is behind
`[Authorize(Roles = "showDiagnostics")]`, so it needs the user's authenticated
session — there is no API key. Two options, in order:

**a. Browser (preferred — verified working 2026-09-19)**

Needs the user signed in to music4dance.net in Chrome with an account holding
the `showDiagnostics` role. Invoke the `claude-in-chrome` skill, load the
browser tools in one `ToolSearch` call, then:

1. `navigate` a new tab to `https://www.music4dance.net/Admin/Diagnostics`
   (any same-origin page works; this one confirms the session by its title).
2. `javascript_tool` — fetch the export and check it before doing anything
   with it:

   ```js
   const r = await fetch('/Admin/Http4xxExportCsv', { credentials: 'same-origin' });
   const t = await r.text();
   window.__csv = t;
   ({ status: r.status, bytes: t.length, isCsv: t.trimStart().startsWith('Url,Status,Count') });
   ```

3. **Only if `isCsv` is true**, hand the file to the browser's downloader
   rather than routing ~60 KB of CSV back through the console — it lands on
   disk directly and never enters context:

   ```js
   const a = document.createElement('a');
   a.href = URL.createObjectURL(new Blob([window.__csv], { type: 'text/csv' }));
   a.download = '4xx-urls-<YYYY-MM-DD>.csv';
   document.body.appendChild(a); a.click(); a.remove();
   ```

4. Move it out of the download directory into `local/`:
   `mv ~/Downloads/4xx-urls-<date>.csv local/`
5. Close the tab.

An unauthenticated session does **not** fail loudly: the fetch follows the
redirect to `/Identity/Account/Login` and returns **status 200 with the login
page's HTML**, which is why step 2 checks the body rather than the status. If
`isCsv` is false, fall back to (b) — don't try to sign in, entering credentials
is out of scope.

**b. Ask the user** to visit `/Admin/Diagnostics`, click "Export CSV", and drop
the file in `local/`.

## Step 2 — Check what production is actually running

**Do this before anything else — it prevents a whole class of wasted work.**

The export applies `IsKnownAttackUrl` at *read* time, so anything the filter
already knows about is absent from a current-build CSV. If patterns that are in
`Http4xxTracker.cs` on `main` still appear in the export, production is running
an **older build**, and everything fixed since that build will still be present
in the data.

Check a handful of distinctive current patterns against the CSV:

```bash
grep -c "/\.env" local/4xx-urls-<date>.csv     # should be 0 on a current build
grep -ci "\.php"  local/4xx-urls-<date>.csv     # should be 0
```

If they're non-zero, find which commit last touched the missing pattern
(`git log -S'<pattern>' -- m4d/Security/Http4xxTracker.cs`) and treat everything
in that commit and later as **not yet deployed**. Say so explicitly in the
report, and don't re-investigate bugs those commits already fixed.

## Step 3 — Filter the CSV through the current filter

Re-apply `main`'s current `IsKnownAttackUrl` to the CSV so you triage only what
would actually survive today. Parse the pattern lists straight out of the C#
source so the script can't drift from the implementation — do not retype them:

```python
import re, io, csv
src = io.open('m4d/Security/Http4xxTracker.cs', encoding='utf-8').read()
def grab(name):
    blk = src.split(name + ' =\n    [', 1)[1].split('];', 1)[0]
    blk = '\n'.join(l.split('//')[0] for l in blk.splitlines())   # drop comments
    return re.findall('"([^"]*)"', blk)
P, S = grab('KnownAttackPathPrefixes'), grab('KnownAttackPathSubstrings')
def attack(u):
    p = u.split('?', 1)[0].lower()
    return any(p.startswith(x.lower()) for x in P) or any(x.lower() in p for x in S)
```

Read the CSV with `encoding='utf-8-sig'` (it has a BOM) and sort what survives
by `Count` descending. Write it to `local/4xx-remaining-<date>.csv` and read the
whole thing — the long tail of 1-hit URLs is where the interesting bugs hide,
and the high-count head is usually benign platform probing.

## Step 4 — Classify every surviving row

Check each pattern against the triage log table **first**. Only investigate what
isn't already there. Every row lands in exactly one bucket:

**Internal bug** — the highest-value find. Tells:
- Our own URL shape with a value missing (`?name=`, `?title=`, `/undefined`)
- A path that is another path with one of *our* paths appended
  (`/song/home/privacypolicy`, `/dances/home/privacypolicy`) — almost always a
  **document-relative link** that only works from the site root. Grep the href
  without its leading slash across `m4d/Views` and `m4d/ClientApp/src`.
- A third-party hostname appearing as a path segment — a protocol-relative
  (`//host/path`) asset URL that some clients resolve against our origin.
- 400s on our own `/api/*` endpoints with ordinary user text in them.

**Internal gap** — a well-known file browsers request unconditionally that we
never served (`/favicon.ico`). Worth fixing even though nothing links to it.

**Bad actor / scanner** — candidate for the filter. See step 5.

**Benign platform probe** — `/.well-known/*`, `/apple-touch-icon*`,
`/sitemap.xml`, `/llms.txt`, `/ai.txt`. Leave visible; note in the log.

**External / mangled** — fabricated or referrer-mangled links for paths that
don't exist anywhere in the codebase (`git grep` the distinctive segment; no hit
means it isn't ours). Ignore.

### Reading 400s vs 404s

They mean different things and must not be lumped together:

- `/api/song/?filter=...` **404** = search ran, no results. Normal.
- `/api/song/?filter=...` **400**, `/api/search?search=...` **400**,
  `/api/suggestion/...` **400** = antiforgery rejection
  (`[ValidateAntiForgeryToken]`, or `SuggestionController.ValidateAntiforgery`).
  A whole typing session at 400 means that page load's token never matched.
  See the antiforgery row in the triage log before re-deriving this.

### Burst signatures

Rows with **identical counts and timestamps seconds apart** are one scripted
sweep, not organic traffic — check `LastSeen` in the original CSV. Case variants
of the same word (`/old`, `/Old`, `/OLD`) are the same tell.

## Step 5 — Widen the filter

Add new patterns to `KnownAttackPathPrefixes` (start-of-path) or
`KnownAttackPathSubstrings` (anywhere), grouped under the existing probe-family
comments. Two rules:

- **Only add a pattern that names a file or route this app has never served and
  never will.** When in doubt, leave it visible and write a log row instead.
- **Never add a short generic segment** a future feature could plausibly use
  (`/login`, `/dashboard`, `/account`, `/mcp`, `/manifest.json`), and never add
  a benign platform probe worth watching (`/sitemap.xml`, `/.well-known/*`).

Prefer the shortest pattern that covers a family — `.env` covers `/admin/.env`,
`/secrets.env` and `aws_credentials.env`; `credentials` covers `.json`, `.yml`,
`.db` and `.git-credentials`. Remember the query string is stripped before
matching, so a substring only ever has to match the path.

Then extend `IsKnownAttackUrl_ClassifiesKnownPatterns` in
`Http4xxTrackerTests.cs` **in both directions**: a `true` row for each family
added, and a `false` row for each must-stay-visible URL you decided against.
Run:

```bash
dotnet test m4d.Tests/m4d.Tests.csproj -p:BaseOutputPath=local/build-out/ --filter "FullyQualifiedName~Http4xx"
```

Finally, re-run the step 3 script against the widened lists and report how many
rows and events the change suppresses.

## Step 6 — Update the triage log

Append a row to the table in `architecture/distributed-attack-mitigation.md` for
**every** pattern triaged this pass, including the ones deliberately left
unfiltered — recording the decision is the point. Columns:
`URL / Pattern | Category | Explanation | Status`.

For internal bugs, the Explanation must say *why* the URL was generated, not
just that it 404ed. Mark fixes `**Internal bug — fixed <Mon D, YYYY>**`.

If the filter's own summary paragraph above the table enumerates patterns
inline, keep it a summary — point at the source rather than duplicating a list
that will drift.

## Step 7 — Report

Cover, in this order:

1. Whether production is current (step 2) and, if not, what is pending deploy.
2. Bugs found and fixed, with file:line.
3. Filter changes and the before/after noise numbers.
4. Anything deliberately left alone, and why.
5. Open items needing a decision from the user (artwork, product calls like
   what a bare `/song/album` should do, anything needing production log access).

## Conventions

- Scratch scripts go in the scratchpad dir; CSVs and derived files go in
  `local/` (gitignored). Never write either to the repo root.
- Don't `git add` the CSVs.
- Commits need both the DCO sign-off and the Claude co-author trailer — see
  the repo `CLAUDE.md`.
